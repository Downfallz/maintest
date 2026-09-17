using System.Security.Cryptography;
using DownfallArena.Application.Agents;
using DownfallArena.Cli.Hosting;
using DownfallArena.Domain.Matches;
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

        var seat1 = Seat(PlayerSlot.Player1, options.Player1Named, options.Player1, options, rules, agents, random, stopping.Token);
        var seat2 = Seat(PlayerSlot.Player2, options.Player2Named, options.Player2, options, rules, agents, random, stopping.Token);
        using var session = await TableSession.StartAsync(services, rules, seed, seat1.Agent, seat2.Agent, stopping.Token);

        // The session's own read side, not the container's: it is the one behind the lock the driver writes
        // through, and a page polls it while the match is advancing.
        var seats = new[] { seat1.Seat, seat2.Seat };
        var api = new TableApi(session, session.Queries, seats);
        var codes = new JoinCodes(seats);
        using var server = new TableServer(options.Bind, options.Port, api, new TableFiles(TableDirectory), codes);

        Announce(server, codes, seats, options, rules);

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
    private static void Announce(TableServer server, JoinCodes codes, IReadOnlyList<TableSeat> seats, CliOptions options, RuleSet rules)
    {
        Console.WriteLine($"Table on {server.Url}");

        // The rule set is named before anything is played. The board game is balanced for 8 to 16 rounds and
        // the engine's default caps at thirty, so a table that took one silently would be testing another
        // game (ADR 0052).
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
        bool named,
        AgentSpec spec,
        CliOptions options,
        RuleSet rules,
        IAgentFactory agents,
        IRandomSource random,
        CancellationToken cancellation)
    {
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

    private static string Describe(PlayerSlot slot, CliOptions options) =>
        (slot == PlayerSlot.Player1 ? options.Player1 : options.Player2).ToString();

    /// <summary>
    /// A seat token. It is not a credential — nothing here has an account — but it must not be guessable from
    /// another browser tab on the same machine, which is what a random 128 bits buys.
    /// </summary>
    private static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
