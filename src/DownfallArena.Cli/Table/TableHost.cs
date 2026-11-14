using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Application.Accounts.Players;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Tables.Ports;
using DownfallArena.Application.Work;
using DownfallArena.Cli.Hosting;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Tables;
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
        var offered = SeatableAgents.Read(WeightsDirectory);
        var levels = BotLevels.OfHost(offered);

        // The operator can seat every bot a level seats (ADR 0180), Easy's included, which no list put forward.
        IReadOnlyList<SeatableAgent> agents = [.. offered, .. levels.All
            .Where(level => offered.All(agent => !string.Equals(agent.Value, level.Agent, StringComparison.OrdinalIgnoreCase)))
            .Select(level => new SeatableAgent(level.Agent, level.Agent, Featured: false))];

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
        var push = keys is null ? null : new PushNotices(sender!, keys.PublicKey, clock) { Record = table => table.RecordAsync() };
        // Finished matches with an account in them, kept beside the accounts for a ladder (ADR 0122, stage 5).
        var results = options.Accounts != AccountsMode.Off && store is not null
            ? await ArtifactStores.OpenResultsAsync(options.Record ?? DefaultRunsDirectory, stopping.Token)
            : null;
        // The index that lists and finds the tables (ADR 0161): Table Storage beside a container, files beside a
        // directory, and this process's own for a host that keeps nothing or whose store's index cannot be opened.
        var index = (store is null ? null : await OpenIndexAsync(options.Record ?? DefaultRunsDirectory, stopping.Token)) ?? new InMemoryTableIndex();
        // Where the bots' turns are taken (ADR 0159, phase 5; ADR 0162): handed to a worker through a queue when the
        // host is told so -- its own, or table-worker's in a process of its own -- and in this process otherwise.
        var mode = TurnsOf(options);
        if (mode == TurnsMode.Worker && !(store is not null && ArtifactStores.IsContainerUrl(options.Record ?? DefaultRunsDirectory)))
        {
            // A directory's queues are this process's own, and a host recording nothing has no store a worker could
            // pick its tables up from: no table-worker could take a turn put on them.
            Console.WriteLine(store is null
                ? "  Turns: this host records nothing a worker could pick its tables up from, so the bots' turns are taken by a worker here (--turns worker needs --record <container-url>)."
                : "  Turns: a directory has no queue another process reads, so the bots' turns are taken by a worker here (--turns worker needs --record <container-url>).");
            mode = TurnsMode.Queue;
        }

        var channels = mode == TurnsMode.Process ? null : ArtifactStores.OpenChannels(options.Record ?? DefaultRunsDirectory, clock);
        var turns = channels is null ? null : await OpenChannelAsync(channels, AgentTurnWorker.Queue, stopping.Token);
        var contents = store is null ? null : await OpenContentsAsync(options.Record ?? DefaultRunsDirectory, stopping.Token);
        var composer = new TableComposer(services, rules, store)
        {
            Contents = contents,
            Turns = turns is null ? new InProcessAgentTurns(clock) : new QueuedAgentTurns(turns, clock) { Said = Console.WriteLine },
            Index = index,
            Agents = agents,
            Levels = levels,
            Push = push,
            Results = results,
            Basis = ReplayBasis.Of(EngineVersion.Current, services.GetRequiredService<IGameResources>(), rules),
        };
        var stored = store is null ? null : new StoredSessions(store);
        // The tables this host holds, and the way to the others: picked up from their records when a request
        // names them, on a host that records any.
        using var registry = new TableRegistry(index) { Loader = stored is null ? null : new TableLoader(stored, composer, clock), Basis = composer.Basis };

        // The operator's door: the platform's login where the host is behind one, a printed token everywhere
        // else. The token is minted the way a pilot's is, and printed the way a pilot's is.
        var gate = options.PlatformAuth ? OperatorGate.BehindPlatform() : OperatorGate.WithToken(TableComposer.Token());
        // The accounts that are the operator once signed in (ADR 0172), a third door beside either.
        var (admins, adminProblem) = OperatorGate.ParseAdmins(options.Admins ?? Environment.GetEnvironmentVariable(AdminsEnvironment));
        if (adminProblem is not null || (admins.Count > 0 && options.Accounts == AccountsMode.Off))
        {
            await Console.Error.WriteLineAsync(adminProblem ?? $"{AdminsEnvironment} names admin accounts, and this host signs nobody in: add '--accounts google' or '--accounts dev'.");
            return 1;
        }

        gate = gate.WithAdmins(admins);
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
        var opened = options.Accounts == AccountsMode.Off
            ? ((AccountApi Api, IAccountStore Store)?)null
            : await OpenAccountsAsync(options, googleClientId, signingKeys, clock, stopping.Token);
        var accounts = opened?.Api;
        FriendsApi? friends = null;
        Friends? friendships = null;
        PlayersApi? players = null;
        PlayerDirectory? directory = null;
        if (accounts is not null)
        {
            // The devices an account is told on (friends plan, stage 1), kept beside the accounts; with keys to
            // sign a notice, something to send one with.
            var devices = new AccountDevices(await ArtifactStores.OpenDevicesAsync(options.Record ?? DefaultRunsDirectory, stopping.Token), clock);
            accounts.Devices = devices;
            accounts.Notices = sender is null ? null : new AccountNotices(sender, devices);
            // Friendships (ADR 0129), beside the accounts too; a deleted account leaves no link on anybody's list.
            friendships = new Friends(await ArtifactStores.OpenFriendsAsync(options.Record ?? DefaultRunsDirectory, stopping.Token), opened!.Value.Store, clock);
            friends = new FriendsApi(friendships, accounts, accounts.Notices);
            accounts.ForgetFriends = id => friendships.ForgetAsync(id);
            // The settings every new game of the account starts from (ADR 0140), beside the accounts too.
            accounts.Preferences = await ArtifactStores.OpenPreferencesAsync(options.Record ?? DefaultRunsDirectory, stopping.Token);
            // When each account last signed in and was last seen (ADR 0174), beside the accounts too, and the
            // operator's list of players read from it, the accounts and the results.
            accounts.Activity = new AccountActivityLog(await ArtifactStores.OpenActivityAsync(options.Record ?? DefaultRunsDirectory, stopping.Token), clock);
            // The host's settings (ADR 0177), beside the accounts too: who may make an account, and the notice.
            accounts.Settings = new HostSettingsCache(await ArtifactStores.OpenHostSettingsAsync(options.Record ?? DefaultRunsDirectory, stopping.Token), clock);
            directory = new PlayerDirectory(opened.Value.Store, accounts.Activity, results, clock);
            players = new PlayersApi(directory, accounts, results)
            {
                Friends = friendships,
                // The operator's: the bot's own name, and the level a player knows it by (ADR 0180).
                BotNamed = spec => levels.OperatorName(spec, LobbyApi.BotName(agents.FirstOrDefault(agent => string.Equals(agent.Value, spec, StringComparison.OrdinalIgnoreCase))?.Label ?? spec)),
            };
        }
        var admin = new AdminApi(registry, composer, gate, clock, described, stored, agents, push)
        {
            Accounts = accounts,
            Players = players,
            Settings = accounts?.Settings is { } settings ? new SettingsApi(settings, clock) : null,
        };
        // A table opened for a friend (ADR 0129): the lobby checks the friendship and tells the friend's devices.
        admin.Lobby.Friends = friendships;
        // A host the hourly job tends lets no request do the job's work (ADR 0162).
        admin.Lobby.Tended = options.Tended;
        admin.Lobby.Notices = accounts?.Notices;
        // How a match ended, to each seat's account (stage 4), named as the cards name them.
        composer.Notices = accounts?.Notices;
        // What the lobbies in view read on, instead of reading on a clock (ADR 0164).
        // With a relay (ADR 0165), what changed is also said to every page and process through it, whichever replica
        // they reach, and pages connect to it rather than to this process's streams.
        var relay = WebPubSubRelay.FromEnvironment();
        Console.WriteLine(relay is null
            ? $"  Relay: none. Pages read this process's own streams, and it looks at the tables it holds every second for what other processes wrote ({WebPubSubRelay.Environment} names a Web PubSub endpoint)."
            : "  Relay: on. Pages and processes hear what moved through Web PubSub, and nothing is looked at on a clock.");
        var lobbyChanges = new LobbyChanges { Relay = relay };
        composer.Lobby = lobbyChanges;
        composer.Relay = relay;
        admin.Lobby.Changes = lobbyChanges;
        admin.Lobby.Relay = relay;
        friends?.Changes = lobbyChanges;
        // The bots' turn queues (A3b), when the turns go through them: their lengths and dead letters in the Jobs tab.
        TurnQueues? queues = null;
        if (channels is not null && turns is not null)
        {
            var poisoned = await OpenChannelAsync(channels, PoisonedWork.QueueOf(turns.Name), stopping.Token);
            var letters = await OpenChannelAsync(channels, DeadLetter.QueueOf(turns.Name), stopping.Token);
            queues = poisoned is null || letters is null ? null : new TurnQueues(turns, poisoned, letters);
            admin.Turns = queues is null ? null : new TurnsApi(queues);
        }

        // The tending runs (A3), kept beside the recordings, and a pass run now from the panel: the hourly job's pass,
        // over this process's tables, said through the relay as the job says it. A store of runs that cannot be opened
        // leaves the panel without its Jobs tab, never the players without the host.
        // The suspended accounts' seats (ADR 0178), which a pass run from the panel offers as the hourly job's does.
        var suspendedSeats = opened is { } held ? new SuspendedSeats(held.Store, admin.Lobby) : null;
        if (store is not null
            && await TableTend.OpenLogAsync(() => ArtifactStores.OpenTendLogAsync(options.Record ?? DefaultRunsDirectory, stopping.Token)) is { } tendLog)
        {
            admin.Jobs = new JobsApi(tendLog, clock, async (now, progress) =>
            {
                var tended = await TableTend.TendAsync(registry, admin.Lobby, now, composer, clock, progress, suspendedSeats);
                if (relay is not null)
                {
                    TableTend.Announce(relay, tended);
                }

                return tended;
            });
        }

        // The Overview (A5): the numbers at a glance, each read from the source its own tab reads.
        admin.Overview = new OverviewApi(registry, composer, clock)
        {
            Players = directory,
            Results = results,
            Tending = admin.Jobs?.Runs,
            Turns = queues,
            Settings = accounts?.Settings,
        };

        composer.NameOf = accounts is null ? null : async id => (await accounts.AccountAsync(id))?.Username.Value;
        // The ladder (ADR 0124), read from the results the composer keeps; bots are named as the lobby names them.
        var leaderboard = accounts is null || results is null
            ? null
            : new LeaderboardApi(results, accounts, clock, levels.PlayerName);
        // A player's own statistics (ADR 0168), read off the same results, named from this host's catalogue.
        var stats = accounts is null || results is null ? null : new StatsApi(results, accounts, services.GetRequiredService<IGameResources>());
        using var server = new TableServer(options.Bind, options.Port, registry, new TableFiles(TableDirectory), admin, clock, stored, accounts, leaderboard, friends, stats) { Tended = options.Tended };

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
            : accounts is null
                ? "  Notices: on. A seat's page offers \"Notify me when it's my turn\"."
                : "  Notices: on. A seat's page offers \"Notify me when it's my turn\"; the lobby offers \"Notify me on this device\" to a signed-in player.");

        Console.WriteLine(options.Tended
            ? "  Tending: by the hourly job (table-tend). Requests close no table that never began and let no invitation go (ADR 0162)."
            : "  Tending: on requests, once a minute at most. --tended leaves it to the hourly job (table-tend).");

        // The place of the table the command line asks for is held first: a table composed into a host whose
        // tables are as many as it takes would be recorded and announced with nothing answering to its codes.
        using var place = options.Lobby ? null : await registry.ReserveAsync(stopping.Token);
        if (!options.Lobby && place is null)
        {
            await Console.Error.WriteLineAsync($"The tables already open are as many as a host takes ({registry.Capacity}): close one from the admin panel (--lobby) first.");
            return 1;
        }

        ConsoleCancelEventHandler stop = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopping.Cancel();
        };
        Console.CancelKeyPress += stop;
        try
        {
            // Serving starts before the open tables are indexed: the container's platform waits a minute for the
            // port to answer and then restarts it (ADR 0080). Until they are, the server answers a key it cannot
            // place yet with "try again in a moment" (TableServer.Starting), which keeps what ADR 0091 asked of
            // it: a page polling a seat never reads it as lost.
            server.Starting = true;
            var serving = server.RunAsync(stopping.Token);
            var working = mode == TurnsMode.Worker && turns is not null
                ? TakenByTheWorker(turns)
                : await StartWorkersAsync(channels, turns, registry, clock, stopping.Token);

            // What other processes write to the tables this host holds -- the worker's turns, the hourly job's
            // expiries -- is heard through the relay when there is one, and looked for every second when there is not
            // (ADR 0162, 0165). A host that records nothing shares its tables with nobody.
            var watching = stored is null ? Task.CompletedTask : WatchAsync(new TableWatch(registry, clock), relay, clock, stopping.Token);
            if (serving.IsFaulted)
            {
                await serving;
            }

            if (stored is not null)
            {
                foreach (var line in await TableRestorer.RestoreAsync(stored, registry, stopping.Token))
                {
                    Console.WriteLine($"  {line}");
                }
            }

            // The table the command line asks for, unless it asks for the admin panel and nothing else.
            if (place is not null)
            {
                var table = await composer.ComposeAsync(TableRequest.Of(options, seed), stopping.Token);
                await registry.AddAsync(table, place, stopping.Token);
                await table.RecordAsync(stopping.Token);
                Announce(server, table);
            }

            server.Starting = false;
            Console.WriteLine(gate.Token is { } token
                ? $"  Admin: {server.Url}admin?token={token}"
                : $"  Admin: {server.Url}admin, behind the platform's sign-in ({OperatorGate.PrincipalHeader} is trusted).");
            if (gate.Admins.Count > 0)
            {
                Console.WriteLine($"  Admin accounts, once signed in: {string.Join(", ", gate.Admins)}.");
            }

            Console.WriteLine(server.IsLoopback
                ? $"  Only this machine can reach it.{Elsewhere()}"
                : "  Reachable from this network. A seat is its code: anyone who has one plays that seat.");

            // Serving stops on Ctrl+C, which is also how a session that ended badly is left readable rather
            // than vanishing: the host keeps serving after a match so its players can read the end screen. The
            // workers stop with it.
            await serving;
            await working;
            await watching;
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
    private static void Announce(TableServer server, PlayedTable table)
    {
        Console.WriteLine(table.Run is { } run
            ? $"  Session {run.SessionId} into '{run.Location}'"
            : $"  Session {table.Id}");

        foreach (var seat in table.Seats)
        {
            var who = seat.Person is null ? table.Session.SeatedName(seat.Slot) : "a person";
            var join = seat.Code is { } code ? $" — joins at {server.Url[..^1]}{JoinCodes.Prefix}{code}" : string.Empty;
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
    /// <summary>
    /// The index of the tables, or none when it cannot be opened: the store refuses its identity the table data
    /// role, which a deployment made before ADR 0161 lacks until its bootstrap is run again. A table is its
    /// record and its journal; the index only finds them, and a host without one serves what it holds.
    /// </summary>
    /// <summary>
    /// Where the content the tables began on is kept (ADR 0163), or none when it cannot be opened: the tables are then
    /// replayed on this host's own content, as before, which is said.
    /// </summary>
    internal static async Task<IArtifactStore?> OpenContentsAsync(string target, CancellationToken cancellationToken)
    {
        try
        {
            return await ArtifactStores.OpenContentAsync(target, cancellationToken);
        }
        catch (Exception failure) when (failure is Azure.RequestFailedException or Azure.Identity.AuthenticationFailedException or IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"  The content the tables began on cannot be kept ({failure.Message}); a table picked up after the content changed is replayed on this host's (ADR 0163).");
            return null;
        }
    }

    private static async Task<ITableIndex?> OpenIndexAsync(string target, CancellationToken cancellationToken)
    {
        try
        {
            return await ArtifactStores.OpenIndexAsync(target, cancellationToken);
        }
        catch (Exception failure) when (failure is Azure.RequestFailedException or Azure.Identity.AuthenticationFailedException or IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"  The index of the tables could not be opened ({failure.Message}); the host keeps its own, which finds what this process holds and what it picks up (ADR 0161, infra/README.md).");
            return null;
        }
    }

    /// <summary>The variable that says where the bots' turns are taken when <c>--turns</c> does not: <c>process</c>, <c>queue</c> or <c>worker</c>.</summary>
    public const string TurnsEnvironment = "DOWNFALL_TURNS";

    /// <summary>Where the bots' turns are taken: as the command line says, or the variable, or in this process.</summary>
    internal static TurnsMode TurnsOf(CliOptions options) =>
        options.Turns ?? CliOptions.TryParseTurns(Environment.GetEnvironmentVariable(TurnsEnvironment)) ?? TurnsMode.Process;

    /// <summary>The bots' turns put on the queue for table-worker, in a process of its own (ADR 0162): no worker here.</summary>
    private static Task TakenByTheWorker(IWorkChannel turns)
    {
        Console.WriteLine($"  Turns: through the queue {turns.Name}, taken by {CliOptions.WorkerCommand} in a process of its own.");
        return Task.CompletedTask;
    }

    /// <summary>A work queue, or none when it cannot be opened: the bots' turns are then taken in this process, which is said.</summary>
    internal static async Task<IWorkChannel?> OpenChannelAsync(IWorkChannels channels, string name, CancellationToken cancellationToken)
    {
        try
        {
            return await channels.OpenAsync(name, cancellationToken);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            Console.WriteLine($"  The queue {name} could not be opened ({failure.Message}); the bots' turns are taken in this process.");
            return null;
        }
    }

    /// <summary>
    /// The two workers of the queued turns (ADR 0159, phase 5): one takes the turns the queue says tables owe, the
    /// other keeps for good what the first gave up on. In a host told <c>--turns queue</c>, and in
    /// <c>table-worker</c> (ADR 0162); none when the turns are taken in this process.
    /// </summary>
    internal static async Task<Task> StartWorkersAsync(IWorkChannels? channels, IWorkChannel? turns, TableRegistry registry, TimeProvider clock, CancellationToken stopping)
    {
        if (channels is null || turns is null)
        {
            Console.WriteLine("  Turns: the bots' turns are taken in this process (--turns queue hands them to a worker).");
            return Task.CompletedTask;
        }

        var poison = await channels.OpenAsync(PoisonedWork.QueueOf(turns.Name), stopping);
        var deadLetters = await channels.OpenAsync(DeadLetter.QueueOf(turns.Name), stopping);
        var worker = new AgentTurnWorker(turns, poison, (run, cancellationToken) => TakerAsync(registry, run, cancellationToken), clock)
        {
            Said = Console.WriteLine,
        };
        var keeper = new DeadLetterWorker(poison, deadLetters, clock, Environment.MachineName) { Said = Console.WriteLine };
        Console.WriteLine($"  Turns: through the queue {turns.Name}, given up on after {AgentTurnWorker.MostDeliveries} deliveries onto {poison.Name}, kept for good on {deadLetters.Name}.");
        return Task.WhenAll(worker.RunAsync(stopping), keeper.RunAsync(stopping));
    }

    /// <summary>
    /// The table a queued turn is owed at, or none when it is gone; a store that is out is thrown, so the worker
    /// retries the item, and gives up on it only as it gives up on any other failure.
    /// </summary>
    internal static async Task<IAgentTurnTaker?> TakerAsync(TableRegistry registry, string run, CancellationToken cancellationToken)
    {
        var found = await registry.FindAsync(run, cancellationToken);
        return found.Unavailable is { } why
            ? throw new IOException($"Session {run} could not be picked up: {why}")
            : found.Table?.Session as IAgentTurnTaker;
    }

    private static string Elsewhere()
    {
        var addresses = NetworkAddresses.OfThisMachine();
        return addresses.Count == 0
            ? " For a phone on the same network, bind this machine's address: --bind <address>."
            : $" For a phone on the same network: --bind {string.Join(" or --bind ", addresses)}.";
    }

    /// <summary>The variable the hosted table reads its Google client id from (ADR 0122).</summary>
    public const string GoogleClientIdEnvironment = "DOWNFALL_GOOGLE_CLIENT_ID";

    /// <summary>The variable a host reads its admin accounts from when <c>--admins</c> is not given (ADR 0172).</summary>
    public const string AdminsEnvironment = "DOWNFALL_ADMINS";

    private static string AccountsLocation(CliOptions options)
    {
        var record = options.Record ?? DefaultRunsDirectory;
        return ArtifactStores.IsContainerUrl(record)
            ? ArtifactStores.AccountsUri(new Uri(record)).ToString()
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(record).TrimEnd(Path.DirectorySeparatorChar)) ?? record, ArtifactStores.AccountsContainer);
    }

    private static async Task<(AccountApi Api, IAccountStore Store)> OpenAccountsAsync(CliOptions options, string? googleClientId, GoogleSigningKeys? keys, TimeProvider clock, CancellationToken cancellationToken)
    {
        var store = await ArtifactStores.OpenAccountsAsync(options.Record ?? DefaultRunsDirectory, cancellationToken);
        var sessions = new AccountSessions(await store.SessionKeyAsync(cancellationToken), clock);
        var verifier = keys is null ? null : new GoogleIdTokenVerifier(googleClientId!, keys, clock);
        return (new AccountApi(new AccountRegistry(store, clock), sessions, options.Accounts, verifier, googleClientId), store);
    }

    /// <summary>What another process wrote to the tables held, heard through the relay or looked for every second.</summary>
    internal static Task WatchAsync(TableWatch watch, IChangeRelay? relay, TimeProvider clock, CancellationToken stopping) =>
        relay is null ? watch.RunAsync(stopping) : new RelayListener(relay, watch, clock).RunAsync(stopping);

}
