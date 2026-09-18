using System.Security.Cryptography;
using System.Text.Json;
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
        // abandoned at its first question still says what it was (ADR 0054, stage 5). A table told
        // --no-record opens nothing and writes nothing: a rule tried out at a table should be able to leave
        // the disk as it found it.
        var run = options.Recording
            ? PlaytestRun.Open(
                options.Record ?? DefaultRunsDirectory,
                new PlaytestSetup(
                    resources,
                    rules,
                    seed,
                    Stamped(PlayerSlot.Player1, seat1.Seat, options),
                    Stamped(PlayerSlot.Player2, seat2.Seat, options)),
                events,
                services.GetRequiredService<TimeProvider>())
            : null;
        if (run is not null)
        {
            await run.StartAsync(catalogue, stopping.Token);
        }

        using var session = await TableSession.StartAsync(services, rules, seed, seat1.Agent, seat2.Agent, run is { } recording ? recording.Wrap : null, stopping.Token);

        // One checkpoint before anybody has tapped anything, so the trace file exists from the start. A
        // session abandoned at its first question is then a readable directory rather than one missing a file,
        // and every checkpoint after this one overwrites it.
        if (run is not null)
        {
            await run.CheckpointAsync(session.MatchId, stopping.Token);
        }

        // The session's own read side, not the container's: it is the one behind the lock the driver writes
        // through, and a page polls it while the match is advancing.
        var seats = new[] { seat1.Seat, seat2.Seat };

        var pilot = new TablePilot(
            Token(),
            (slot, wanted) => Seating(slot == PlayerSlot.Player1 ? seat1.Seat : seat2.Seat, wanted, options, rules, agents, random));

        var api = new TableApi(session, session.Queries, seats, catalogue, events, run, pilot);
        var codes = new JoinCodes(seats);
        using var server = new TableServer(options.Bind, options.Port, api, new TableFiles(TableDirectory), codes, run);

        Announce(server, codes, seats, options, rules, run, pilot);

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
            if (run is not null)
            {
                await CloseAsync(run, session);
            }


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
    private static void Announce(TableServer server, JoinCodes codes, IReadOnlyList<TableSeat> seats, CliOptions options, RuleSet rules, PlaytestRun? run, TablePilot? pilot)
    {
        Console.WriteLine($"Table on {server.Url}");

        // Said out loud either way. A table that is recording names where, and one that is not says so before
        // anybody plays a session they meant to keep.
        Console.WriteLine(run is null
            ? "  Recording nothing: no session directory, no notes, no trace (--no-record)."
            : $"  Recording session {run.SessionId} into '{run.Directory}'");

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

        // The pilot's own token, said once and never mixed into a seat's link: the operator is at this machine
        // and the players are not, so it goes on this console and nowhere a page could pick it up.
        if (pilot is { } flying)
        {
            Console.WriteLine($"  pilot: {TableApi.TokenHeader}: {flying.Token}");
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
    /// What the run stamp calls a seat when the session opens: whoever is sitting down, and nobody else.
    /// </summary>
    /// <remarks>
    /// It names the first occupant only, and the session adds to it as seats actually change hands
    /// (<see cref="PlaytestRun.Wrap" />). A handover used to be composed here from the round it was configured
    /// with, which said <c>Greedy&gt;human:mk@3</c> of a session abandoned in round 2 — a person who never
    /// played, in the axis <c>compare-stamps</c> reads. One stamp built from what happened beats two built
    /// from what was asked for and what was configured.
    /// </remarks>
    private static string Stamped(PlayerSlot slot, TableSeat seat, CliOptions options) =>
        seat.Person is null || options.Handover is not null ? Describe(slot, options) : PersonName(options);

    /// <summary>
    /// Who the pilot may seat: any agent spec the CLI understands, and the word <c>person</c> for the seat's
    /// own player.
    /// </summary>
    /// <remarks>
    /// A seat that no person ever joined has nobody to hand back to, and says so rather than inventing a
    /// second player nobody holds the token for. A name nothing can be built from -- a typo in a spec, a
    /// weights file that is not there -- is the operator getting a name wrong, so it is answered as "nobody
    /// of that name can sit here" rather than as a broken host: the match is fine and the next attempt is one
    /// line away.
    /// </remarks>
    private static Occupant? Seating(TableSeat seat, string wanted, CliOptions options, RuleSet rules, IAgentFactory agents, IRandomSource random)
    {
        if (string.Equals(wanted, "person", StringComparison.OrdinalIgnoreCase))
        {
            return seat.Person is { } person ? new Occupant(person, PersonName(options)) : null;
        }

        try
        {
            // Resolved, not just parsed: for a file-backed agent the resolved spec carries a fingerprint of
            // the weights or the policy as they are on disk now. Naming it from the text the pilot typed would
            // stamp two sessions played against different weights at the same path as the same agent, and the
            // comparison that reads the agents axis would call them comparable. It is also what `GameSession`
            // does to the agents named at startup, so a seat taken mid-match is named the same way as one
            // seated at the start.
            var spec = agents.Resolve(AgentSpec.Parse(wanted));
            return new Occupant(agents.Create(spec, rules, random), spec.ToString());
        }
        catch (Exception failure) when (failure is ArgumentException or IOException or JsonException or InvalidDataException)
        {
            // InvalidDataException among them: a policy file that exists but holds something else is the
            // operator naming the wrong file, which is the same mistake as naming no file at all. Without it
            // the host answers a 500 for what is an expected bad input.
            return null;
        }
    }

    /// <summary>
    /// What a record calls the person at this table. One helper rather than one expression per caller: the run
    /// stamp and every step's <c>decidedBy</c> have to agree, and a name spelled twice is a name that drifts.
    /// </summary>
    private static string PersonName(CliOptions options) => options.Who is { Length: > 0 } who ? $"human:{who}" : "human";

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
        var bot = new Occupant(agents.Create(spec, rules, random), spec.ToString());
        if (named)
        {
            return (new SeatAgent(bot), new TableSeat(slot, Token(), Person: null));
        }

        var human = new HumanSeat(cancellation);
        var person = new Occupant(human, PersonName(options));

        // A handover is a swap like any other: the bot sits down and the seat is told to hand over at the top
        // of the round named. Without one the person plays from the first decision.
        var seat = new SeatAgent(options.Handover is null ? person : bot);
        if (options.Handover is { } round)
        {
            seat.SwapAt(person, round);
        }

        return (seat, new TableSeat(slot, Token(), human));
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
