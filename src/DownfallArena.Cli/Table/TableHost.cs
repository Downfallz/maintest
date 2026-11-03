using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Hosting;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Accounts;
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
        using var registry = new TableRegistry();

        // "Your turn" to a seat's phone (ADR 0118), when the host has keys to sign it with: the file named, or
        // the hosted table's variable. A key pair that is not one is one line here, not a refusal per notice.
        PushKeys? keys;
        try
        {
            keys = PushKeys.Load(options.PushKeys, Environment.GetEnvironmentVariable(PushKeys.Environment));
        }
        catch (Exception failure) when (failure is IOException or InvalidDataException or JsonException)
        {
            await Console.Error.WriteLineAsync($"The push keys could not be read: {failure.Message}");
            return 1;
        }

        using var sender = keys is null ? null : new WebPushSender(keys);
        var push = keys is null ? null : new PushNotices(sender!, keys.PublicKey, clock) { Record = table => table.RecordAsync(registry.Codes) };
        // Finished matches with an account in them, kept beside the accounts for a ladder (ADR 0122, stage 5).
        var results = options.Accounts != AccountsMode.Off && store is not null
            ? await ArtifactStores.OpenResultsAsync(options.Record ?? DefaultRunsDirectory, stopping.Token)
            : null;
        var composer = new TableComposer(services, rules, store) { Agents = agents, Push = push, Results = results };
        var stored = store is null ? null : new StoredSessions(store);

        // The operator's door: the platform's login where the host is behind one, a printed token everywhere
        // else. The token is minted the way a pilot's is, and printed the way a pilot's is.
        var gate = options.PlatformAuth ? OperatorGate.BehindPlatform() : OperatorGate.WithToken(TableComposer.Token());
        var described = RuleSetFile.Describe(rules, options.Rules);
        // The players' sign-in (ADR 0122), kept beside the recordings. A Google sign-in without its client id is
        // one line here rather than a page whose button never draws.
        var googleClientId = options.GoogleClientId ?? Environment.GetEnvironmentVariable(GoogleClientIdEnvironment);
        if (options.Accounts == AccountsMode.Google && string.IsNullOrWhiteSpace(googleClientId))
        {
            await Console.Error.WriteLineAsync($"'--accounts google' needs the Google client id: --google-client-id <id>, or {GoogleClientIdEnvironment}.");
            return 1;
        }

        using var google = options.Accounts == AccountsMode.Google ? new HttpClient() : null;
        using var signingKeys = google is null ? null : new GoogleSigningKeys(google, clock);
        var accounts = options.Accounts == AccountsMode.Off
            ? null
            : await OpenAccountsAsync(options, googleClientId, signingKeys, clock, stopping.Token);
        var admin = new AdminApi(registry, composer, gate, clock, described, stored, agents, push) { Accounts = accounts };
        // The ladder (ADR 0124), read from the results the composer keeps; bots are named as the lobby names them.
        var leaderboard = accounts is null || results is null
            ? null
            : new LeaderboardApi(results, accounts, clock, spec => LobbyApi.BotName(agents.FirstOrDefault(agent => string.Equals(agent.Value, spec, StringComparison.OrdinalIgnoreCase))?.Label ?? spec));
        using var server = new TableServer(options.Bind, options.Port, registry, new TableFiles(TableDirectory), admin, clock, stored, accounts, leaderboard);

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
        Console.WriteLine(accounts is null
            ? "  Accounts: off. Seats are their tokens and codes; --accounts google signs players in (ADR 0122)."
            : $"  Accounts: {AccountApi.Named(accounts.Mode)}{(accounts.GoogleClientId is { } id ? $" (client {id})" : "")}, kept in '{AccountsLocation(options)}'.");
        Console.WriteLine(push is null
            ? $"  Notices: off. A phone is told \"your turn\" when the host has push keys: --push-keys <file>, or {PushKeys.Environment} (push-keys prints a pair)."
            : "  Notices: on. A seat's page offers \"Notify me when it's my turn\".");

        // The place of the table the command line asks for is held first: the rebuild below fills the host up
        // to its bound, and a table composed into a full host would be recorded and announced with nothing
        // answering to its codes. An empty host always has the place.
        using var place = options.Lobby ? null : registry.Reserve() ?? throw new InvalidOperationException("An empty host has a place for its first table.");

        ConsoleCancelEventHandler stop = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopping.Cancel();
        };
        Console.CancelKeyPress += stop;
        try
        {
            // Serving starts before the rebuild: the container's platform waits a minute for the port to answer
            // and then restarts it (ADR 0080), and replaying every table an earlier host left open can take longer
            // than that -- a host that rebuilt before serving never came up again. Until the rebuild is done the
            // server answers a table it does not hold yet with "try again in a moment" (TableServer.Starting),
            // which keeps what ADR 0091 asked of it: a page polling a seat never reads it as lost.
            server.Starting = true;
            var serving = server.RunAsync(stopping.Token);
            if (serving.IsFaulted)
            {
                await serving;
            }

            // And before the table the command line asks for: that one is recorded as open the moment it is, and
            // rebuilt after itself it would be the same match twice.
            if (stored is not null)
            {
                foreach (var line in await TableRestorer.RestoreAsync(stored, composer, registry, clock, stopping.Token))
                {
                    Console.WriteLine($"  {line}");
                }
            }

            // The table the command line asks for, unless it asks for the admin panel and nothing else.
            if (place is not null)
            {
                var table = await composer.ComposeAsync(TableRequest.Of(options, seed), stopping.Token);
                registry.Add(table, place);
                await table.RecordAsync(registry.Codes, stopping.Token);
                Announce(server, registry, table);
            }

            server.Starting = false;
            Console.WriteLine(gate.Token is { } token
                ? $"  Admin: {server.Url}admin?token={token}"
                : $"  Admin: {server.Url}admin, behind the platform's sign-in ({OperatorGate.PrincipalHeader} is trusted).");

            Console.WriteLine(server.IsLoopback
                ? $"  Only this machine can reach it.{Elsewhere()}"
                : "  Reachable from this network. A seat is its code: anyone who has one plays that seat.");

            // Serving stops on Ctrl+C, which is also how a session that ended badly is left readable rather
            // than vanishing: the host keeps serving after a match so its players can read the end screen.
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
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Ctrl+C while the tables were being rebuilt: nothing was half-served, and the records are as they were.
            return 0;
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

        // One link carrying every human seat's invitation, for the case where the two people share this
        // machine's browser: the page follows whichever seat the match asks, and typing two codes on one device
        // gets to the same place. A bot's seat is on nothing -- nobody needs to read a bot's board.
        var people = table.Seats.Where(seat => seat.Key is not null).ToList();
        if (people.Count > 1)
        {
            Console.WriteLine($"  Both seats here: {server.Url}?{string.Join('&', people.Select(seat => $"{seat.Name}={seat.Key}"))}");
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

    /// <summary>The variable the hosted table reads its Google client id from (ADR 0122).</summary>
    public const string GoogleClientIdEnvironment = "DOWNFALL_GOOGLE_CLIENT_ID";

    private static string AccountsLocation(CliOptions options)
    {
        var record = options.Record ?? DefaultRunsDirectory;
        return ArtifactStores.IsContainerUrl(record)
            ? ArtifactStores.AccountsUri(new Uri(record)).ToString()
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(record).TrimEnd(Path.DirectorySeparatorChar)) ?? record, ArtifactStores.AccountsContainer);
    }

    private static async Task<AccountApi> OpenAccountsAsync(CliOptions options, string? googleClientId, GoogleSigningKeys? keys, TimeProvider clock, CancellationToken cancellationToken)
    {
        var store = await ArtifactStores.OpenAccountsAsync(options.Record ?? DefaultRunsDirectory, cancellationToken);
        var sessions = new AccountSessions(await store.SessionKeyAsync(cancellationToken), clock);
        var verifier = keys is null ? null : new GoogleIdTokenVerifier(googleClientId!, keys, clock);
        return new AccountApi(new AccountRegistry(store, clock), sessions, options.Accounts, verifier, googleClientId);
    }
}
