using System.Security.Cryptography;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Cli.Hosting;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Randomness;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The <c>table</c> command: one match, two seats, one screen. It composes a session, puts a person or a bot
/// in each seat, and serves the page and that seat's API until the match ends or someone stops it.
/// </summary>
internal static class TableHost
{
    public const string TableDirectory = "table";

    /// <summary>Where a session is written when <c>--record</c> names nowhere else.</summary>
    public const string DefaultRunsDirectory = "runs/playtest";

    public static async Task<int> RunAsync(IServiceProvider services, CliOptions options, RuleSet rules, int seed)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(Path.Combine(TableDirectory, "index.html")))
        {
            await Console.Error.WriteLineAsync($"'{TableDirectory}/index.html' not found. Run the table from the repository root.");
            return 1;
        }

        using var stopping = new CancellationTokenSource();
        var random = services.GetRequiredService<IRandomSource>();
        var agents = services.GetRequiredService<IAgentFactory>();
        var resources = services.GetRequiredService<IGameResources>();
        var events = services.GetRequiredService<MatchTraceRecorder>();

        var seat1 = Seat(PlayerSlot.Player1, options, rules, agents, random, stopping.Token);
        var seat2 = Seat(PlayerSlot.Player2, options, rules, agents, random, stopping.Token);

        // Built once, from the resources this match is playing: a card the page prints is the card the engine
        // resolves, and a tuning pass is a rebuild and a restart rather than a change to the page (ADR 0054).
        var catalogue = CatalogueProjection.Build(resources, rules);

        // Opened before the match, so the files exist before anything can be decided into them: a session
        // abandoned at its first question still says what it was (ADR 0054, stage 5).
        var run = PlaytestRun.Open(
            options.Record ?? DefaultRunsDirectory,
            new PlaytestSetup(
                resources,
                rules,
                seed,
                Stamped(PlayerSlot.Player1, seat1.Seat, options),
                Stamped(PlayerSlot.Player2, seat2.Seat, options)),
            events,
            services.GetRequiredService<TimeProvider>());
        await run.StartAsync(catalogue, stopping.Token);

        using var session = await TableSession.StartAsync(services, rules, seed, seat1.Agent, seat2.Agent, run.Wrap, stopping.Token);

        // One checkpoint before anybody has tapped anything, so the trace file exists from the start. A
        // session abandoned at its first question is then a readable directory rather than one missing a file,
        // and every checkpoint after this one overwrites it.
        await run.CheckpointAsync(session.MatchId, stopping.Token);

        // The session's own read side, not the container's: it is the one behind the lock the driver writes
        // through, and a page polls it while the match is advancing.
        var seats = new[] { seat1.Seat, seat2.Seat };
        var api = new TableApi(session, session.Queries, seats, catalogue, events, run);
        var codes = new JoinCodes(seats);
        using var server = new TableServer(options.Bind, options.Port, api, new TableFiles(TableDirectory), codes, run);

        Announce(server, codes, seats, options, rules, run);

        ConsoleCancelEventHandler stop = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopping.Cancel();
        };
        Console.CancelKeyPress += stop;
        try
        {
            var serving = server.RunAsync(stopping.Token);
            await Task.WhenAny(serving, session.Outcome);

            // Closed the moment the match has an outcome rather than when the host stops. The host keeps
            // serving so two people can read the end screen and write a comment, and a session page opened
            // from there has to show a finished run rather than the zero-count manifest it was opened with.
            await CloseAsync(run, session);

            // The match is over, but the page has not read the outcome yet. Serving stops on Ctrl+C, which is
            // also how a session that ended badly is left readable rather than vanishing.
            await serving;
            return 0;
        }
        catch (System.Net.HttpListenerException exception)
        {
            // An address this machine does not answer on, or a port something else already holds. Both are a
            // line to read and a flag to change, not a stack trace across a table with two people waiting.
            await Console.Error.WriteLineAsync($"Cannot serve {server.Url}: {exception.Message}.{Elsewhere()}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= stop;
        }
    }

    /// <summary>
    /// What a player reads before the first tap: where the table is, which rule set it is playing, who is in
    /// each seat, and the code that seat's player types. Everything a session needs to be joined and to be
    /// reproduced is on these lines, because the alternative is a playtest that nobody can place afterwards.
    /// </summary>
    private static void Announce(TableServer server, JoinCodes codes, IReadOnlyList<TableSeat> seats, CliOptions options, RuleSet rules, PlaytestRun run)
    {
        Console.WriteLine($"Table on {server.Url}");
        Console.WriteLine($"  Recording session {run.SessionId} into '{run.Directory}'");

        // The rule set is named before anything is played. The board game is balanced for 8 to 16 rounds and
        // the engine's default caps at thirty, so a table that took one silently would be testing another
        // game (ADR 0054).
        Console.WriteLine($"  {RuleSetFile.Describe(rules, options.Rules)}");
        foreach (var seat in seats)
        {
            var who = seat.Person is null ? Describe(seat.Slot, options) : "a person";
            var join = codes.Of(seat) is { } code ? $" — joins at {server.Url[..^1]}{JoinCodes.Prefix}{code}" : string.Empty;
            Console.WriteLine($"  {seat.Name}: {who}{join}");
        }

        // One link carrying every human seat's token, for the case where the two people share this machine's
        // browser: the page follows whichever seat the match asks, and typing two codes on one device gets to
        // the same place. A bot's token is on nothing -- nobody needs to read a bot's board.
        var people = seats.Where(seat => seat.Person is not null).ToList();
        if (people.Count > 1)
        {
            Console.WriteLine($"  Both seats here: {server.Url}?{string.Join('&', people.Select(seat => $"{seat.Name}={seat.Token}"))}");
        }

        Console.WriteLine(server.IsLoopback
            ? $"  Only this machine can reach it.{Elsewhere()}"
            : "  Reachable from this network. A seat is its code: anyone who has one plays that seat.");
    }

    /// <summary>
    /// Closes the session's dataset, and only on a match that reached an outcome. A session someone walked away
    /// from keeps the zero-count manifest it was opened with and the trace checkpointed at the last decision:
    /// that says "this was abandoned" out loud, where a manifest counting the steps of an unfinished match
    /// would read exactly like a finished one (<c>docs/tabletop/app-roadmap.md</c>, stage 5).
    /// </summary>
    private static async Task CloseAsync(PlaytestRun run, TableSession session)
    {
        if (!session.Outcome.IsCompletedSuccessfully || session.Outcome.Result.IsFailure)
        {
            Console.WriteLine($"  Session {run.SessionId} was not finished; '{run.Directory}' holds it as far as it got.");
            return;
        }

        var board = await session.Queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, PlayerSlot.Player1));
        if (board.IsFailure)
        {
            Console.WriteLine($"  Session {run.SessionId} ended but its final board could not be read: {board.Error.Message}");
            return;
        }

        await run.FinishAsync(session.MatchId, board.Value);
        Console.WriteLine($"  Session {run.SessionId} written to '{run.Directory}'");
    }

    /// <summary>
    /// What the run stamp calls a seat: the agent's own name for a bot, and <c>human</c> — with the initials
    /// <c>--who</c> gave, when it gave any — for a person, so <c>compare-stamps</c> reports the agents axis
    /// between two sessions played by different people.
    /// </summary>
    private static string Stamped(PlayerSlot slot, TableSeat seat, CliOptions options)
    {
        if (seat.Person is null)
        {
            return Describe(slot, options);
        }

        return options.Who is { Length: > 0 } who ? $"human:{who}" : "human";
    }

    /// <summary>The addresses a phone could be told, so choosing one is not a trip to the network settings.</summary>
    private static string Elsewhere()
    {
        var addresses = NetworkAddresses.OfThisMachine();
        return addresses.Count == 0
            ? " For a phone on the same network, bind this machine's address: --bind <address>."
            : $" For a phone on the same network: --bind {string.Join(" or --bind ", addresses)}.";
    }

    private static (SeatAgent Agent, TableSeat Seat) Seat(
        PlayerSlot slot,
        CliOptions options,
        RuleSet rules,
        IAgentFactory agents,
        IRandomSource random,
        CancellationToken cancellation)
    {
        var (named, spec) = Chosen(slot, options);
        var bot = agents.Create(spec, rules, random);
        if (named)
        {
            return (new SeatAgent(bot), new TableSeat(slot, Token(), Person: null));
        }

        var person = new HumanSeat(cancellation);
        var seat = new SeatAgent(person);

        // A handover seats the bot first and swaps at the round it names; without one the person plays from
        // the first decision.
        if (options.Handover is { } round)
        {
            seat.Seat(new HandoverAgent(seat, bot, person, round));
        }

        return (seat, new TableSeat(slot, Token(), person));
    }

    /// <summary>
    /// The agent this slot was told to play, and whether it was told at all. The table seats a person in every
    /// slot no agent was named for, and "the option was absent" is the only thing that says so.
    /// </summary>
    private static (bool Named, AgentSpec Spec) Chosen(PlayerSlot slot, CliOptions options) =>
        slot == PlayerSlot.Player1 ? (options.Player1Named, options.Player1) : (options.Player2Named, options.Player2);

    private static string Describe(PlayerSlot slot, CliOptions options) => Chosen(slot, options).Spec.ToString();

    /// <summary>
    /// A seat token. It is not a credential — nothing here has an account — but it must not be guessable from
    /// another browser tab on the same machine, which is what a random 128 bits buys.
    /// </summary>
    private static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
