using DownfallArena.Cli.Hosting;
using DownfallArena.Domain.Matches;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The <c>table</c> command: a host of tables, each one match with two seats. It composes the table the
/// command line asks for, if it asks for one, and serves the pages, the admin panel and every table's API until
/// someone stops it (ADR 0054, ADR 0081).
/// </summary>
internal static class TableHost
{
    public const string TableDirectory = "table";

    /// <summary>Where a session is written when <c>--record</c> names nowhere else.</summary>
    public const string DefaultRunsDirectory = "runs/playtest";

    /// <summary>Where the weights files are, as an agent spec names them: what the panel offers a seat.</summary>
    public const string WeightsDirectory = "learning/weights";

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
        var clock = services.GetRequiredService<TimeProvider>();

        // A table told --no-record opens nothing and writes nothing: a rule tried out at a table should be
        // able to leave the disk as it found it.
        var store = options.Recording ? await ArtifactStores.OpenAsync(options.Record ?? DefaultRunsDirectory, stopping.Token) : null;
        var agents = SeatableAgents.Read(WeightsDirectory);
        var composer = new TableComposer(services, rules, store) { Agents = agents };
        var stored = store is null ? null : new StoredSessions(store);
        using var registry = new TableRegistry();

        // The operator's door: the platform's login where the host is behind one, a printed token everywhere
        // else. The token is minted the way a pilot's is, and printed the way a pilot's is.
        var gate = options.PlatformAuth ? OperatorGate.BehindPlatform() : OperatorGate.WithToken(TableComposer.Token());
        var described = RuleSetFile.Describe(rules, options.Rules);
        var admin = new AdminApi(registry, composer, gate, clock, described, stored, agents);
        using var server = new TableServer(options.Bind, options.Port, registry, new TableFiles(TableDirectory), admin, clock, stored);

        Console.WriteLine($"Table on {server.Url}");

        // Said out loud either way. A host that is recording names where, and one that is not says so before
        // anybody plays a session they meant to keep.
        Console.WriteLine(store is null
            ? "  Recording nothing: no session directory, no notes, no trace (--no-record)."
            : $"  Recording sessions into '{store.LocationOf("<id>")}'");

        // The rule set is named before anything is played. The board game is balanced for 10 to 15 rounds and
        // the engine's default caps at thirty, so a table that took one silently would be testing another
        // game (ADR 0054).
        Console.WriteLine($"  {described}");

        // The table the command line asks for, unless it asks for the admin panel and nothing else.
        if (!options.Lobby)
        {
            var table = await composer.ComposeAsync(TableRequest.Of(options, seed), stopping.Token);
            registry.TryAdd(table);
            Announce(server, registry, table);
        }

        Console.WriteLine(gate.Token is { } token
            ? $"  Admin: {server.Url}admin?token={token}"
            : $"  Admin: {server.Url}admin, behind the platform's sign-in ({OperatorGate.PrincipalHeader} is trusted).");

        Console.WriteLine(server.IsLoopback
            ? $"  Only this machine can reach it.{Elsewhere()}"
            : "  Reachable from this network. A seat is its code: anyone who has one plays that seat.");

        ConsoleCancelEventHandler stop = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopping.Cancel();
        };
        Console.CancelKeyPress += stop;
        try
        {
            // Serving stops on Ctrl+C, which is also how a session that ended badly is left readable rather
            // than vanishing: the host keeps serving after a match so its players can read the end screen.
            await server.RunAsync(stopping.Token);
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
    /// What a player reads before the first tap: which session this is, who is in each seat, and the code
    /// that seat's player types. Everything a session needs to be joined and to be reproduced is on these
    /// lines, because the alternative is a playtest that nobody can place afterwards.
    /// </summary>
    private static void Announce(TableServer server, TableRegistry registry, PlayedTable table)
    {
        Console.WriteLine(table.Run is { } run
            ? $"  Session {run.SessionId} into '{run.Location}'"
            : $"  Session {table.Id}");

        foreach (var seat in table.Seats)
        {
            var who = seat.Person is null ? table.Session.Seat(seat.Slot).Seated.Name : "a person";
            var join = registry.Codes.Of(seat) is { } code ? $" — joins at {server.Url[..^1]}{JoinCodes.Prefix}{code}" : string.Empty;
            Console.WriteLine($"  {seat.Name}: {who}{join}");
        }

        // The pilot's own token, said once and never mixed into a seat's link: the operator is at this machine
        // and the players are not, so it goes on this console and nowhere a page could pick it up. The page
        // link is printed beside it because a token nobody can find the door for is a feature nobody uses;
        // the header form stays, because the swap is as often one curl from a shell.
        Console.WriteLine($"  Pilot: {server.Url}pilot?token={table.Pilot.Token}");
        Console.WriteLine($"    or from a shell, as '{TableApi.TokenHeader}: {table.Pilot.Token}'");

        // One link carrying every human seat's token, for the case where the two people share this machine's
        // browser: the page follows whichever seat the match asks, and typing two codes on one device gets to
        // the same place. A bot's token is on nothing -- nobody needs to read a bot's board.
        var people = table.Seats.Where(seat => seat.Person is not null).ToList();
        if (people.Count > 1)
        {
            Console.WriteLine($"  Both seats here: {server.Url}?{string.Join('&', people.Select(seat => $"{seat.Name}={seat.Token}"))}");
        }
    }

    /// <summary>The addresses a phone could be told, so choosing one is not a trip to the network settings.</summary>
    private static string Elsewhere()
    {
        var addresses = NetworkAddresses.OfThisMachine();
        return addresses.Count == 0
            ? " For a phone on the same network, bind this machine's address: --bind <address>."
            : $" For a phone on the same network: --bind {string.Join(" or --bind ", addresses)}.";
    }
}
