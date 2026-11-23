using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Application.Accounts.Players;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Admin;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;
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
using DownfallArena.Infrastructure.Resources;
using DownfallArena.Infrastructure.Tables;
using DownfallArena.Infrastructure.Work;
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
        await using var host = await ComposeAsync(services, options, rules, sender is null ? null : new Pushing(sender, keys!.PublicKey), TableDirectory, WebPubSubRelay.FromEnvironment(), stopping.Token);
        if (host is null)
        {
            return 1;
        }

        var (server, registry, composer, store, stored) = (host.Server, host.Registry, host.Composer, host.Store, host.Stored);
        var (accounts, push, gate, described) = (host.Accounts, host.Push, host.Gate, host.Described);

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
                : "  Notices: on. A seat's page offers \"Notify me when it's my turn\"; Settings offer \"Pop-ups on this device\" to a signed-in player.");

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
            var running = await StartAsync(host, clock, stopping.Token);
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
            await running;
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
    /// What a composed host runs beside its server, until stopped: the workers of the bots' turns (unless table-worker
    /// takes them), the notices' workers, and the watch of the tables it holds. What <see cref="RunAsync" /> starts, and
    /// what the tests start to see each is started (#1159).
    /// </summary>
    internal static async Task<Task> StartAsync(ComposedHost host, TimeProvider clock, CancellationToken stopping)
    {
        ArgumentNullException.ThrowIfNull(host);
        var working = host.Mode == TurnsMode.Worker && host.Turns is not null
            ? TakenByTheWorker(host.Turns)
            : await StartWorkersAsync(host.Channels, host.Turns, host.Registry, clock, stopping);
        var noticing = await StartNoticesAsync(host.Channels, host.Notices, host.Push, host.Registry, clock, stopping, host.NoticeCounts);

        // What other processes write to the tables this host holds -- the worker's turns, the hourly job's
        // expiries -- is heard through the relay when there is one, and looked for every second when there is not
        // (ADR 0162, 0165). A host that records nothing shares its tables with nobody.
        var watching = host.Stored is null ? Task.CompletedTask : WatchAsync(new TableWatch(host.Registry, clock), host.Relay, clock, stopping);
        return Task.WhenAll(working, noticing, watching);
    }

    /// <summary>What sends a notice to a device, and the public key a page subscribes with: a host given push keys.</summary>
    internal sealed record Pushing(IPushSender Sender, string PublicKey);

    /// <summary>
    /// Everything the <c>table</c> command serves, composed and wired as it runs, not yet serving: the stores, the
    /// accounts, the door every notice goes through (ADR 0201), the queues and the server. What <see cref="RunAsync" />
    /// serves, and what the tests compose to see that a setter only this composition root assigns is assigned (#1109).
    /// None, said on the console's error stream, when the options cannot be served.
    /// </summary>
    /// <param name="pushing">The sender and the public key of a host given push keys; none on a host without.</param>
    /// <param name="files">The directory the pages are served from.</param>
    /// <param name="relay">Where what moved is said (ADR 0165): the one the environment names, a recording one in the tests (#1153); none.</param>
    internal static async Task<ComposedHost?> ComposeAsync(IServiceProvider services, CliOptions options, RuleSet rules, Pushing? pushing, string files, IChangeRelay? relay, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        var kept = new ComposedHost.Kept();
        try
        {
            var composed = await ComposeAsync(services, options, rules, pushing, new Composing(services, options, files, kept, services.GetRequiredService<TimeProvider>()) { Relay = relay }, cancellationToken);
            if (composed is null)
            {
                await kept.DisposeAsync();
            }

            return composed;
        }
        catch
        {
            await kept.DisposeAsync();
            throw;
        }
    }

    private static async Task<ComposedHost?> ComposeAsync(IServiceProvider services, CliOptions options, RuleSet rules, Pushing? pushing, Composing composing, CancellationToken cancellationToken)
    {
        var (kept, clock) = (composing.Kept, composing.Clock);
        var tables = await ComposeTablesAsync(services, options, rules, pushing, kept, cancellationToken);
        var (composer, registry) = (tables.Composer, tables.Registry);
        if (await GateAsync(options) is not { } gate)
        {
            return null;
        }

        var described = RuleSetFile.Describe(rules, options.Rules);
        // The players' sign-in (ADR 0122), kept beside the recordings. A Google sign-in without its client id is
        // one line here rather than a page whose button never draws.
        var googleClientId = options.GoogleClientId ?? Environment.GetEnvironmentVariable(GoogleClientIdEnvironment);
        if (options.Accounts == AccountsMode.Google && string.IsNullOrWhiteSpace(googleClientId))
        {
            await Console.Error.WriteLineAsync($"'--accounts google' needs the Google client id: --google-client-id <id>, or {GoogleClientIdEnvironment}.");
            return null;
        }

        var google = kept.Add(options.Accounts == AccountsMode.Google ? new HttpClient() : null);
        var signingKeys = kept.Add(google is null ? null : new GoogleSigningKeys(google, clock));
        var signedIn = options.Accounts == AccountsMode.Off
            ? null
            : await ComposeAccountsAsync(tables, pushing, await OpenAccountsAsync(options, googleClientId, signingKeys, clock, cancellationToken), clock, cancellationToken);
        var accounts = signedIn?.Api;
        var admin = new AdminApi(registry, composer, gate, clock, described, tables.Stored, tables.Agents, tables.Push)
        {
            Accounts = accounts,
            Players = signedIn?.Players,
            Settings = accounts?.Settings is { } settings ? new SettingsApi(settings, clock) : null,
        };
        // A table opened for a friend (ADR 0129): the lobby checks the friendship and tells the friend's devices.
        admin.Lobby.Friends = signedIn?.Friendships;
        // A host the hourly job tends lets no request do the job's work (ADR 0162).
        admin.Lobby.Tended = options.Tended;
        admin.Lobby.Notifier = signedIn?.Notifier;
        // A signed-in seat's subscription is its account's device too (ADR 0202).
        admin.Lobby.Devices = accounts?.Devices;
        // How a match ended, to each seat's account (stage 4), named as the cards name them.
        composer.Notifier = signedIn?.Notifier;
        var (relay, lobbyChanges) = (composing.Relay, Relayed(composing.Relay, composer, admin.Lobby, signedIn));
        var tabs = await ComposeTabsAsync(tables, admin, signedIn, relay, kept, clock, cancellationToken);
        composer.NameOf = accounts is null ? null : async id => (await accounts.AccountAsync(id, CancellationToken.None))?.Username.Value;
        var server = kept.Add(await ServerAsync(composing, tables, admin, signedIn, tabs, gate, cancellationToken));
        return new ComposedHost(kept)
        {
            Server = server,
            Registry = registry,
            Composer = composer,
            Admin = admin,
            Store = tables.Store,
            Stored = tables.Stored,
            Accounts = accounts,
            AccountStore = signedIn?.Store,
            Friends = signedIn?.Friends,
            Notifier = signedIn?.Notifier,
            Push = tables.Push,
            Gate = gate,
            Relay = relay,
            Changes = lobbyChanges,
            Described = described,
            Mode = tables.Queues.Mode,
            Channels = tables.Queues.Channels,
            Turns = tables.Queues.Turns,
            Notices = tables.Queues.Notices,
            NoticeCounts = tabs.NoticeCounts,
            Chores = tabs.Chores,
        };
    }

    /// <summary>What a host is composed from, the same for every part: the services, the options, the pages' directory, what is disposed with it, and the clock.</summary>
    private sealed record Composing(IServiceProvider Services, CliOptions Options, string Files, ComposedHost.Kept Kept, TimeProvider Clock)
    {
        public IChangeRelay? Relay { get; init; }
    }

    /// <summary>Where the bots' turns and the notices go (ADR 0159, 0162, 0203): through the queues, or in this process.</summary>
    private sealed record Queues(TurnsMode Mode, IWorkChannels? Channels, IWorkChannel? Turns, IWorkChannel? Notices);

    /// <summary>The tables' side of a host: the store, the bots it seats, the queues, the composer and the registry.</summary>
    private sealed record Tables(TableComposer Composer, TableRegistry Registry, Queues Queues, IReadOnlyList<SeatableAgent> Agents, BotLevels Levels)
    {
        public IArtifactStore? Store { get; init; }

        public StoredSessions? Stored { get; init; }

        public PushNotices? Push { get; init; }

        public IMatchResults? Results { get; init; }

        /// <summary>Where the stores beside the recordings are opened: what <c>--record</c> names, or the default directory.</summary>
        public required string Target { get; init; }
    }

    /// <summary>The accounts' side of a host that signs players in: the accounts, the door every notice goes through, friends and players.</summary>
    private sealed record SignedInParts(AccountApi Api, IAccountStore Store, AccountNotifier Notifier, Friends Friendships, FriendsApi Friends, PlayerDirectory Directory, PlayersApi Players);

    /// <summary>The admin tabs' parts the server is built with too: the turns' queues, the notices' counts, feedback and notes.</summary>
    private sealed record Tabs(TurnQueues? Queues, NoticeCounts? NoticeCounts, FeedbackIndex? FeedbackIndex, FeedbackWrites FeedbackWrites, FeedbackMutes FeedbackMutes, OperatorNotes Notes)
    {
        /// <summary>What a pass run from the Jobs tab tends; none without the tab.</summary>
        public TableTend.Chores? Chores { get; init; }
    }

    private static async Task<Tables> ComposeTablesAsync(IServiceProvider services, CliOptions options, RuleSet rules, Pushing? pushing, ComposedHost.Kept kept, CancellationToken cancellationToken)
    {
        var clock = services.GetRequiredService<TimeProvider>();
        var target = options.Record ?? DefaultRunsDirectory;
        // A table told --no-record opens nothing and writes nothing: a rule tried out at a table should be
        // able to leave the disk as it found it.
        var store = options.Recording ? await ArtifactStores.OpenAsync(target, cancellationToken) : null;
        var offered = SeatableAgents.Read(WeightsDirectory);
        var levels = BotLevels.OfHost(offered);

        // The operator can seat every bot a level seats (ADR 0180), Easy's included, which no list put forward.
        IReadOnlyList<SeatableAgent> agents = [.. offered, .. levels.All
            .Where(level => offered.All(agent => !string.Equals(agent.Value, level.Agent, StringComparison.OrdinalIgnoreCase)))
            .Select(level => new SeatableAgent(level.Agent, level.Agent, Featured: false))];

        var push = pushing is null ? null : new PushNotices(pushing.Sender, pushing.PublicKey, clock) { Record = table => table.RecordAsync(CancellationToken.None) };
        // Finished matches with an account in them, kept beside the accounts for a ladder (ADR 0122, stage 5).
        var results = options.Accounts != AccountsMode.Off && store is not null
            ? await ArtifactStores.OpenResultsAsync(target, cancellationToken)
            : null;
        // The index that lists and finds the tables (ADR 0161): Table Storage beside a container, files beside a
        // directory, and this process's own for a host that keeps nothing or whose store's index cannot be opened.
        var index = (store is null ? null : await OpenIndexAsync(target, cancellationToken)) ?? new InMemoryTableIndex();
        var queues = await OpenQueuesAsync(options, store, pushing, clock, cancellationToken);
        push?.Outbox = queues.Notices;
        var composer = new TableComposer(services, rules, store)
        {
            Contents = store is null ? null : await OpenContentsAsync(target, cancellationToken),
            Turns = queues.Turns is null ? new InProcessAgentTurns(clock) : new QueuedAgentTurns(queues.Turns, clock) { Said = Console.WriteLine },
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
        var registry = kept.Add(new TableRegistry(index) { Loader = stored is null ? null : new TableLoader(stored, composer, clock), Basis = composer.Basis });
        return new Tables(composer, registry, queues, agents, levels) { Store = store, Stored = stored, Push = push, Results = results, Target = target };
    }

    /// <summary>
    /// Where the bots' turns are taken (ADR 0159, phase 5; ADR 0162): handed to a worker through a queue when the host is
    /// told so -- its own, or table-worker's in a process of its own -- and in this process otherwise. The notices go
    /// through a queue beside the turns' (ADR 0203), sent off the request by this host and by table-worker; a host whose
    /// turns are taken in process sends them in the request, as before.
    /// </summary>
    private static async Task<Queues> OpenQueuesAsync(CliOptions options, IArtifactStore? store, Pushing? pushing, TimeProvider clock, CancellationToken cancellationToken)
    {
        var target = options.Record ?? DefaultRunsDirectory;
        var mode = TurnsOf(options);
        if (mode == TurnsMode.Worker && !(store is not null && ArtifactStores.IsContainerUrl(target)))
        {
            // A directory's queues are this process's own, and a host recording nothing has no store a worker could
            // pick its tables up from: no table-worker could take a turn put on them.
            Console.WriteLine(store is null
                ? "  Turns: this host records nothing a worker could pick its tables up from, so the bots' turns are taken by a worker here (--turns worker needs --record <container-url>)."
                : "  Turns: a directory has no queue another process reads, so the bots' turns are taken by a worker here (--turns worker needs --record <container-url>).");
            mode = TurnsMode.Queue;
        }

        if (mode == TurnsMode.Process)
        {
            return new Queues(mode, null, null, null);
        }

        var channels = ArtifactStores.OpenChannels(target, clock);
        var turns = await OpenChannelAsync(channels, AgentTurnWorker.Queue, cancellationToken);
        var notices = pushing is null ? null : await OpenChannelAsync(channels, QueuedNotice.Queue, cancellationToken, "the notices are sent in the request that gives them");
        return new Queues(mode, channels, turns, notices);
    }

    /// <summary>
    /// The operator's door: the platform's login where the host is behind one, a printed token everywhere else, and the
    /// accounts that are the operator once signed in (ADR 0172). None, said on the console's error stream, when the
    /// admin accounts cannot be read or no account can sign in.
    /// </summary>
    private static async Task<OperatorGate?> GateAsync(CliOptions options)
    {
        // The token is minted the way a pilot's is, and printed the way a pilot's is.
        var gate = options.PlatformAuth ? OperatorGate.BehindPlatform() : OperatorGate.WithToken(TableComposer.Token());
        var (admins, adminProblem) = OperatorGate.ParseAdmins(options.Admins ?? Environment.GetEnvironmentVariable(AdminsEnvironment));
        if (adminProblem is not null || (admins.Count > 0 && options.Accounts == AccountsMode.Off))
        {
            await Console.Error.WriteLineAsync(adminProblem ?? $"{AdminsEnvironment} names admin accounts, and this host signs nobody in: add '--accounts google' or '--accounts dev'.");
            return null;
        }

        return gate.WithAdmins(admins);
    }

    /// <summary>The accounts' side, opened beside the recordings and wired to the door every notice goes through (ADR 0201).</summary>
    private static async Task<SignedInParts> ComposeAccountsAsync(Tables tables, Pushing? pushing, (AccountApi Api, IAccountStore Store) opened, TimeProvider clock, CancellationToken cancellationToken)
    {
        var (accounts, store, target) = (opened.Api, opened.Store, tables.Target);
        // The devices an account is told on (friends plan, stage 1), kept beside the accounts; with keys to
        // sign a notice, something to send one with.
        var devices = new AccountDevices(await ArtifactStores.OpenDevicesAsync(target, cancellationToken), clock);
        accounts.Devices = devices;
        accounts.Notices = pushing is null ? null : new AccountNotices(pushing.Sender, devices);
        // The bell's news (ADR 0201), beside the accounts too, kept whether or not the host can push.
        accounts.NoticeLog = new AccountNoticeLog(await ArtifactStores.OpenNoticesAsync(target, cancellationToken), clock);
        // The settings every new game of the account starts from (ADR 0140), beside the accounts too, with what pops
        // up on its devices (ADR 0202), which the door reads before each push.
        accounts.Preferences = await ArtifactStores.OpenPreferencesAsync(target, cancellationToken);
        var notifier = new AccountNotifier(accounts.NoticeLog, accounts.Notices)
        {
            Exists = async (id, token) => await store.FindAsync(id, token) is not null,
            Preferences = accounts.Preferences,
            Outbox = tables.Queues.Notices,
        };
        // A signed-in seat's "your turn" goes through the door, to its account's devices (ADR 0202).
        tables.Push?.Accounts = notifier;
        // Friendships (ADR 0129), beside the accounts too; a deleted account leaves no link on anybody's list.
        var friendships = new Friends(await ArtifactStores.OpenFriendsAsync(target, cancellationToken), store, clock);
        var friends = new FriendsApi(friendships, accounts, notifier);
        accounts.ForgetFriends = id => friendships.ForgetAsync(id, CancellationToken.None);
        // When each account last signed in and was last seen (ADR 0174), beside the accounts too, and the
        // operator's list of players read from it, the accounts and the results.
        accounts.Activity = new AccountActivityLog(await ArtifactStores.OpenActivityAsync(target, cancellationToken), clock);
        // The host's settings (ADR 0177), beside the accounts too: who may make an account, and the notice.
        accounts.Settings = new HostSettingsCache(await ArtifactStores.OpenHostSettingsAsync(target, cancellationToken), clock);
        var directory = new PlayerDirectory(store, accounts.Activity, tables.Results, clock);
        var (levels, agents) = (tables.Levels, tables.Agents);
        var players = new PlayersApi(directory, accounts, tables.Results)
        {
            Friends = friendships,
            // The operator's: the bot's own name, and the level a player knows it by (ADR 0180).
            BotNamed = spec => levels.OperatorName(spec, LobbyApi.BotName(agents.FirstOrDefault(agent => string.Equals(agent.Value, spec, StringComparison.OrdinalIgnoreCase))?.Label ?? spec)),
        };
        return new SignedInParts(accounts, store, notifier, friendships, friends, directory, players);
    }

    /// <summary>
    /// What the lobbies in view read on, instead of reading on a clock (ADR 0164). With a relay (ADR 0165), what changed
    /// is also said to every page and process through it, whichever replica they reach, and pages connect to it rather
    /// than to this process's streams.
    /// </summary>
    private static LobbyChanges Relayed(IChangeRelay? relay, TableComposer composer, LobbyApi lobby, SignedInParts? signedIn)
    {
        Console.WriteLine(relay is null
            ? $"  Relay: none. Pages read this process's own streams, and it looks at the tables it holds every second for what other processes wrote ({WebPubSubRelay.Environment} names a Web PubSub endpoint)."
            : "  Relay: on. Pages and processes hear what moved through Web PubSub, and nothing is looked at on a clock.");
        var lobbyChanges = new LobbyChanges { Relay = relay };
        composer.Lobby = lobbyChanges;
        composer.Relay = relay;
        lobby.Changes = lobbyChanges;
        lobby.Relay = relay;
        signedIn?.Friends.Changes = lobbyChanges;
        signedIn?.Notifier.Changes = lobbyChanges;
        signedIn?.Api.Changes = lobbyChanges;
        return lobbyChanges;
    }

    /// <summary>The admin panel's tabs: the queues (A3b, N6), feedback (ADR 0189), the Jobs (A3), the notes (A6) and the Overview (A5).</summary>
    private static async Task<Tabs> ComposeTabsAsync(Tables tables, AdminApi admin, SignedInParts? signedIn, IChangeRelay? relay, ComposedHost.Kept kept, TimeProvider clock, CancellationToken cancellationToken)
    {
        var (store, target, accounts) = (tables.Store, tables.Target, signedIn?.Api);
        var (queues, noticeCounts) = await QueueTabsAsync(tables, admin, clock, cancellationToken);

        // The players' bug reports and ideas (ADR 0189), kept beside the recordings in a store of their own; a pass
        // lets go of what was closed 180 days ago.
        var feedbackIndex = store is null ? null : await OpenFeedbackAsync(target, clock, cancellationToken);
        var feedback = feedbackIndex?.Records;
        var feedbackWrites = kept.Add(new FeedbackWrites());
        if (feedback is not null && accounts is not null)
        {
            var forget = new ForgetFeedbackAccount(feedback, feedbackWrites);
            accounts.ForgetFeedback = id => forget.HandleAsync(id.ToString(), CancellationToken.None);
        }

        var chores = await JobsTabAsync(tables, admin, signedIn, relay, feedbackIndex, clock, cancellationToken);

        // What operators do from the panel (A6), kept beside the recordings and read in the Activity tab. A log that
        // cannot be opened leaves the actions on the console alone, never the panel without them.
        var notes = store is null ? OperatorNotes.None : await OpenNotesAsync(target, clock, cancellationToken);
        admin.Notes = notes;
        admin.Diagnosis = tables.Stored is null ? null : new TableDiagnosisApi(tables.Registry, tables.Composer, tables.Stored) { Turns = queues, Notes = notes };
        signedIn?.Players.Notes = notes;
        admin.Turns?.Notes = notes;
        admin.Jobs?.Notes = notes;
        admin.Settings?.Notes = notes;

        // The Overview (A5): the numbers at a glance, each read from the source its own tab reads.
        admin.Overview = new OverviewApi(tables.Registry, tables.Composer, clock)
        {
            Feedback = feedbackIndex is null ? null : new CountWaitingFeedback(feedbackIndex, clock),
            Players = signedIn?.Directory,
            Results = tables.Results,
            Tending = admin.Jobs?.Runs,
            Turns = queues,
            Settings = accounts?.Settings,
        };

        // The Feedback tab (feedback plan, R3) reads an account's mute kept with its activity, a guest's in this process's memory.
        return new Tabs(queues, noticeCounts, feedbackIndex, feedbackWrites, new FeedbackMutes(accounts?.Activity), notes) { Chores = chores };
    }

    /// <summary>
    /// The bots' turn queues (A3b), when the turns go through them: their lengths and dead letters in the Jobs tab. And
    /// what the notices worker sent, had refused and found gone (N6), a day at a time beside the recordings, which the
    /// worker app adds to as well; in memory where this host records nothing, as its queues are its own. A store that
    /// cannot be opened leaves the counts out, never the notices.
    /// </summary>
    private static async Task<(TurnQueues? Queues, NoticeCounts? NoticeCounts)> QueueTabsAsync(Tables tables, AdminApi admin, TimeProvider clock, CancellationToken cancellationToken)
    {
        var (channels, turns, notices) = (tables.Queues.Channels, tables.Queues.Turns, tables.Queues.Notices);
        TurnQueues? queues = null;
        if (channels is not null && turns is not null)
        {
            queues = await TurnQueuesAsync(channels, turns, cancellationToken);
            admin.Turns = queues is null ? null : new TurnsApi(queues);
        }

        var noticeCounts = notices is null ? null : await OpenNoticeCountsAsync(tables.Store is null ? null : tables.Target, clock, cancellationToken);
        if (channels is not null && notices is not null && await TurnQueuesAsync(channels, notices, cancellationToken) is { } noticeQueues)
        {
            admin.Notices = new NoticesApi(noticeQueues, noticeCounts);
        }

        return (queues, noticeCounts);
    }

    /// <summary>A queue with its poison queue and its dead letters, or none when either cannot be opened.</summary>
    private static async Task<TurnQueues?> TurnQueuesAsync(IWorkChannels channels, IWorkChannel queue, CancellationToken cancellationToken)
    {
        var poisoned = await OpenChannelAsync(channels, PoisonedWork.QueueOf(queue.Name), cancellationToken);
        var letters = await OpenChannelAsync(channels, DeadLetter.QueueOf(queue.Name), cancellationToken);
        return poisoned is null || letters is null ? null : new TurnQueues(queue, poisoned, letters);
    }

    /// <summary>
    /// The tending runs (A3), kept beside the recordings, and a pass run now from the panel: the hourly job's pass, over
    /// this process's tables, said through the relay as the job says it, the suspended accounts' seats offered (ADR 0178)
    /// as the job's does. A store of runs that cannot be opened leaves the panel without its Jobs tab, never the players
    /// without the host.
    /// </summary>
    private static async Task<TableTend.Chores?> JobsTabAsync(Tables tables, AdminApi admin, SignedInParts? signedIn, IChangeRelay? relay, FeedbackIndex? feedbackIndex, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (tables.Store is null || await TableTend.OpenLogAsync(() => ArtifactStores.OpenTendLogAsync(tables.Target, cancellationToken)) is not { } tendLog)
        {
            return null;
        }

        var chores = new TableTend.Chores(tables.Registry, admin.Lobby, clock)
        {
            Composer = tables.Composer,
            Suspended = signedIn is null ? null : new SuspendedSeats(signedIn.Store, admin.Lobby),
            Feedback = feedbackIndex is null ? null : new LetGoOfClosedFeedback(feedbackIndex.Records, clock),
            FeedbackIndex = feedbackIndex,
            Notices = signedIn?.Api.NoticeLog,
        };
        admin.Jobs = new JobsApi(tendLog, clock, async (now, progress) =>
        {
            var tended = await TableTend.TendAsync(chores, now, progress);
            if (relay is not null)
            {
                TableTend.Announce(relay, tended);
            }

            return tended;
        });
        return chores;
    }

    /// <summary>
    /// The match scenarios (ADR 0179): cut by the operator from the recorded sessions and kept beside them, and practised
    /// by signed-in players in this process's memory, over the services built for their content. The repository's own
    /// (S7) are read beside them, each played on the content it carries.
    /// </summary>
    private static async Task<(ScenarioStore? Store, ScenarioPractices? Practices)> ScenariosAsync(Tables tables, ComposedHost.Kept kept, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (tables.Store is null)
        {
            return (null, null);
        }

        var shipped = RepositoryScenarios.Read(RepositoryScenarios.DefaultDirectory);
        foreach (var one in shipped.All)
        {
            tables.Composer.Carry(one.Document.Content, one.Document.Schema!.Value);
        }

        var scenarioStore = await OpenScenariosAsync(tables.Target, clock, cancellationToken, shipped);
        if (scenarioStore is not null && shipped.All.Count > 0)
        {
            Console.WriteLine($"  Scenarios from {RepositoryScenarios.DefaultDirectory}/: {string.Join(", ", shipped.All.Select(one => one.File))}.");
        }

        return (scenarioStore, kept.Add(scenarioStore is null ? null : new ScenarioPractices(tables.Composer, scenarioStore, clock)));
    }

    /// <summary>The ladder, a player's statistics, the scenarios and feedback the players reach, and the server that answers them.</summary>
    private static async Task<TableServer> ServerAsync(Composing composing, Tables tables, AdminApi admin, SignedInParts? signedIn, Tabs tabs, OperatorGate gate, CancellationToken cancellationToken)
    {
        var (services, options, files, kept, clock) = (composing.Services, composing.Options, composing.Files, composing.Kept, composing.Clock);
        var (accounts, results, composer, stored) = (signedIn?.Api, tables.Results, tables.Composer, tables.Stored);
        // The ladder (ADR 0124), read from the results the composer keeps; bots are named as the lobby names them.
        var leaderboard = accounts is null || results is null
            ? null
            : new LeaderboardApi(results, accounts, clock, tables.Levels.PlayerName);
        // A player's own statistics (ADR 0168), read off the same results, named from this host's catalogue.
        var stats = accounts is null || results is null ? null : new StatsApi(results, accounts, services.GetRequiredService<IGameResources>(), services.GetRequiredService<MutationArt>(), services.GetService<SpellArt>() ?? SpellArt.None);
        var (scenarioStore, practices) = await ScenariosAsync(tables, kept, clock, cancellationToken);

        // The Feedback tab (feedback plan, R3): the operator reads, marks and moves feedback, and mutes a sender.
        var feedbackDesk = tabs.FeedbackIndex is null ? null : FeedbackDesk.Open(tabs.FeedbackIndex, tabs.FeedbackMutes, tabs.FeedbackWrites, clock);
        (admin.Scenarios, admin.Feedback) = CuttingTabs(scenarioStore, stored, composer, feedbackDesk, tabs.Notes, clock);
        var feedback = tabs.FeedbackIndex?.Records;
        var sendFeedback = feedback is null ? null : new SendFeedback(feedback, new GuestAddresses(), tabs.FeedbackWrites, clock, tabs.FeedbackMutes);
        return new TableServer(options.Bind, options.Port, tables.Registry, new TableFiles(files), admin, clock, stored, accounts, leaderboard, signedIn?.Friends, stats)
        {
            Feedback = sendFeedback is null ? null : new FeedbackApi(sendFeedback, new AskFeedbackMuted(tabs.FeedbackMutes, clock), tables.Registry) { Accounts = accounts, Practices = practices },
            Tended = options.Tended,
            Scenarios = scenarioStore is null || practices is null || accounts is null ? null : new ScenariosApi(scenarioStore, practices, accounts) { Admins = gate.Admins, Stored = stored },
            Practices = practices,
        };
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

    /// <summary>
    /// The panel's Scenarios and Feedback tabs, built together (#737): the Feedback tab's Reproduce (feedback plan, R4)
    /// cuts a bug report's round through the Scenarios tab's own cutting, and without it the route is a 404 and the
    /// sheet offers no Reproduce. Each is none on a host that keeps no scenarios, no sessions or no feedback.
    /// </summary>
    internal static (ScenarioAdminApi? Scenarios, FeedbackAdminApi? Feedback) CuttingTabs(ScenarioStore? scenarios, StoredSessions? stored, TableComposer composer, FeedbackDesk? feedback, OperatorNotes notes, TimeProvider clock)
    {
        var cutting = scenarios is null || stored is null ? null : new ScenarioAdminApi(scenarios, stored, composer) { Notes = notes };
        return (cutting, feedback is null ? null : new FeedbackAdminApi(feedback, stored, clock) { Notes = notes, Scenarios = cutting });
    }

    /// <summary>The addresses a phone could be told, so choosing one is not a trip to the network settings.</summary>
    /// <summary>
    /// The index of the tables, or none when it cannot be opened: the store refuses its identity the table data
    /// role, which a deployment made before ADR 0161 lacks until its bootstrap is run again. A table is its
    /// record and its journal; the index only finds them, and a host without one serves what it holds.
    /// </summary>
    /// <summary>
    /// Where the match scenarios are kept (ADR 0179), or none when it cannot be opened: the host then cuts and serves
    /// none, which is said, and plays every game as before.
    /// </summary>
    internal static async Task<ScenarioStore?> OpenScenariosAsync(string target, TimeProvider clock, CancellationToken cancellationToken, RepositoryScenarios? shipped = null)
    {
        try
        {
            return new ScenarioStore(await ArtifactStores.OpenScenariosAsync(target, cancellationToken), clock, shipped);
        }
        catch (Exception failure) when (failure is Azure.RequestFailedException or Azure.Identity.AuthenticationFailedException or IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"  The match scenarios cannot be kept ({failure.Message}): none can be cut or practised at this host.");
            return null;
        }
    }

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

    /// <summary>Where feedback is kept (ADR 0189), or none when it cannot be opened: the host then takes none, which is said.</summary>
    internal static async Task<FeedbackIndex?> OpenFeedbackAsync(string target, TimeProvider clock, CancellationToken cancellationToken)
    {
        try
        {
            return await ArtifactStores.OpenFeedbackAsync(target, clock, cancellationToken);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            Console.WriteLine($"  Feedback cannot be kept ({failure.Message}): players can send none at this host.");
            return null;
        }
    }

    /// <summary>The operator log (A6), or none when it cannot be opened, which the console says.</summary>
    private static async Task<OperatorNotes> OpenNotesAsync(string target, TimeProvider clock, CancellationToken cancellationToken)
    {
        try
        {
            return new OperatorNotes(new OperatorActions(await ArtifactStores.OpenOperatorLogAsync(target, cancellationToken), clock));
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            Console.WriteLine($"  The operator log could not be opened ({failure.Message}): what operators do is said on the console alone.");
            return OperatorNotes.None;
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
    /// <param name="otherwise">What happens instead, as the line says it.</param>
    internal static async Task<IWorkChannel?> OpenChannelAsync(IWorkChannels channels, string name, CancellationToken cancellationToken, string otherwise = "the bots' turns are taken in this process")
    {
        try
        {
            return await channels.OpenAsync(name, cancellationToken);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            Console.WriteLine($"  The queue {name} could not be opened ({failure.Message}); {otherwise}.");
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
    /// The notices worker's counts (N6) beside what <paramref name="target" /> names, or in this process's memory for
    /// none; none at all, said on the console, when the store cannot be opened.
    /// </summary>
    internal static async Task<NoticeCounts?> OpenNoticeCountsAsync(string? target, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (target is null)
        {
            return new NoticeCounts(new InMemoryNoticeCountStore(), clock);
        }

        try
        {
            return new NoticeCounts(await ArtifactStores.OpenNoticeCountsAsync(target, cancellationToken), clock)
            {
                ForgetFailed = failure => Console.WriteLine($"  The notices' counts older than a week could not be let go of; tried again tomorrow: {HttpHost.OneLine(failure)}"),
            };
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            Console.WriteLine($"  The notices' counts could not be opened ({HttpHost.OneLine(failure)}); the notices are sent and not counted.");
            return null;
        }
    }

    /// <summary>
    /// The two workers of the notices queue (ADR 0203): one sends what it holds -- a notice to one device of an account,
    /// "your turn" once its grace is over -- and the other keeps for good what the first gave up on. In the API and in
    /// <c>table-worker</c>; none on a host with no queue or no push keys, which sends in the request.
    /// </summary>
    internal static async Task<Task> StartNoticesAsync(IWorkChannels? channels, IWorkChannel? notices, PushNotices? push, TableRegistry registry, TimeProvider clock, CancellationToken stopping, NoticeCounts? counts = null)
    {
        if (channels is null || notices is null || push is null)
        {
            Console.WriteLine("  Notices: sent in the request that gives them (a queue and --push-keys send them off it).");
            return Task.CompletedTask;
        }

        IWorkChannel poison;
        IWorkChannel deadLetters;
        try
        {
            poison = await channels.OpenAsync(PoisonedWork.QueueOf(notices.Name), stopping);
            deadLetters = await channels.OpenAsync(DeadLetter.QueueOf(notices.Name), stopping);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            // Nothing here would read what the door queues: it sends in the request instead.
            push.Outbox = null;
            push.Accounts?.Outbox = null;
            Console.WriteLine($"  The queues beside {notices.Name} could not be opened ({HttpHost.OneLine(failure)}); the notices are sent in the request that gives them.");
            return Task.CompletedTask;
        }

        var worker = new NoticeWorker(notices, poison, clock)
        {
            Devices = push.Accounts?.Push,
            Turns = async (due, cancellationToken) =>
            {
                // A table held here may not have been looked at for a while (TableWatch looks only at those asked for
                // lately): brought up to date first, or let go of and picked up again, so a later asking is not read as
                // an answered one. A look that cannot be made throws, and the item is tried again.
                await new TableWatch(registry, clock).MovedAsync(due.Table, cancellationToken);
                var found = await registry.FindAsync(due.Table, cancellationToken);
                if (found.Unavailable is { } why)
                {
                    throw new IOException($"Session {due.Table} could not be picked up: {why}");
                }

                if (found.Table is { } table)
                {
                    await push.TurnDueAsync(table, due);
                }
            },
            Said = Console.WriteLine,
            Counts = counts,
        };
        var keeper = new DeadLetterWorker(poison, deadLetters, clock, Environment.MachineName) { Said = Console.WriteLine, KeptFor = NoticeWorker.DeadLettersKeptFor };
        Console.WriteLine($"  Notices: through the queue {notices.Name}, given up on after {NoticeWorker.MostDeliveries} tries onto {poison.Name}, kept a week on {deadLetters.Name}.");
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
