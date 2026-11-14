using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Application.Learning;
using DownfallArena.Application.Tables.Ports;
using DownfallArena.Application.Tables.Tending;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The <c>table-tend</c> command (ADR 0162, phase 6): one pass of the work that is due on the tables, over the
/// index and the records, and then it exits. What the hourly job runs, so nothing that waits for a time to pass
/// waits for a request: the sweep closes, through its record, every table that never began and was opened twenty
/// hours ago, an invitation nobody answered in a day is let go of, a game whose player let their time to answer
/// run out ends with them losing (ADR 0169), and a begun game whose seat its player left and nobody took in a day
/// ends with that seat giving it up (ADR 0170). The tables it picks up to do so take no bot turn here: that is the
/// worker's.
/// </summary>
internal static class TableTend
{
    /// <summary>What one pass did.</summary>
    internal sealed record Tended(IReadOnlyList<string> Swept, int Expired)
    {
        /// <summary>The tables ended because their player let their time to answer run out (ADR 0169).</summary>
        public IReadOnlyList<string> TimedOut { get; init; } = [];

        /// <summary>The tables whose player was told a quarter of their time to answer is left (ADR 0169).</summary>
        public IReadOnlyList<string> Warned { get; init; } = [];

        /// <summary>The begun tables ended because nobody took the seat their player left in a day (ADR 0170).</summary>
        public IReadOnlyList<string> Abandoned { get; init; } = [];

        /// <summary>The suspended accounts whose seats were offered to other players, a day after (ADR 0178).</summary>
        public IReadOnlyList<string> Released { get; init; } = [];
    }

    /// <summary>How often the job runs (infra/main.bicep): the warning is sent by the one pass of that interval that finds a quarter left.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public static async Task<int> RunAsync(IServiceProvider services, CliOptions options, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        var clock = services.GetRequiredService<TimeProvider>();
        var target = options.Record ?? TableHost.DefaultRunsDirectory;
        PushKeys? keys;
        try
        {
            keys = PushKeys.Load(options.PushKeys, Environment.GetEnvironmentVariable(PushKeys.Environment));
        }
        catch (Exception failure) when (failure is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            // A game it times out still ends and is still kept; its players are told nothing.
            await Console.Error.WriteLineAsync($"The push keys could not be read, so no notice is sent: {failure.Message}");
            keys = null;
        }

        using var sender = keys is null ? null : new WebPushSender(keys);
        try
        {
            var store = await ArtifactStores.OpenAsync(target);
            var index = await ArtifactStores.OpenIndexAsync(target);
            var composer = new TableComposer(services, rules, store)
            {
                Results = options.Accounts == AccountsMode.Off ? null : await ArtifactStores.OpenResultsAsync(target),
                Contents = await TableHost.OpenContentsAsync(target, CancellationToken.None),
                Turns = new NoAgentTurns(),
                Index = index,
                Basis = ReplayBasis.Of(EngineVersion.Current, services.GetRequiredService<IGameResources>(), rules),
            };
            // A game it times out ends here (ADR 0169): its result is kept for the ladder and each seat's account is
            // told how it ended, as the worker does for a game its turn ends.
            if (sender is not null && options.Accounts != AccountsMode.Off)
            {
                var accounts = await ArtifactStores.OpenAccountsAsync(target);
                composer.Notices = new AccountNotices(sender, new AccountDevices(await ArtifactStores.OpenDevicesAsync(target), clock));
                composer.NameOf = async id => (await accounts.FindAsync(id))?.Username.Value;
            }

            var stored = new StoredSessions(store);
            using var registry = new TableRegistry(index) { Loader = new TableLoader(stored, composer, clock), Basis = composer.Basis };

            // Whether two players are still friends is what lets an invitation between them stand (ADR 0129).
            var friends = options.Accounts == AccountsMode.Off
                ? null
                : new Friends(await ArtifactStores.OpenFriendsAsync(target), await ArtifactStores.OpenAccountsAsync(target), clock);
            // What the pass closes and lets go of is said to the pages and the API through the relay, when there is one
            // (ADR 0165), and the job waits for it to have gone before it exits.
            var relay = WebPubSubRelay.FromEnvironment();
            composer.Relay = relay;
            composer.Lobby = relay is null ? null : new LobbyChanges { Relay = relay };
            var lobby = new LobbyApi(registry, composer, clock) { Friends = friends, Changes = composer.Lobby };
            // The seats of the accounts suspended a day ago (ADR 0178), offered to other players.
            var suspended = options.Accounts == AccountsMode.Off
                ? null
                : await OpenSuspendedAsync(target, lobby);

            // Every run is kept for the admin panel's Jobs tab (A3), the one that failed too. A log that cannot be
            // opened costs the run its record, never the pass: the tables are tended all the same.
            var log = await OpenLogAsync(() => ArtifactStores.OpenTendLogAsync(target));
            var (tended, _) = await RecordedAsync(log, TendTrigger.Schedule, by: null, clock, (now, progress) => TendAsync(registry, lobby, now, composer, clock, progress, suspended));
            if (relay is not null)
            {
                Announce(relay, tended);
                await Task.WhenAny(relay.SentAsync(), Task.Delay(TimeSpan.FromSeconds(10), clock));
            }

            Console.WriteLine($"Tended '{target}': {tended.Swept.Count} table{(tended.Swept.Count == 1 ? string.Empty : "s")} that never began closed, {tended.Expired} invitation{(tended.Expired == 1 ? string.Empty : "s")} that ran out let go of, {tended.TimedOut.Count} game{(tended.TimedOut.Count == 1 ? string.Empty : "s")} timed out, {tended.Abandoned.Count} left by a player ended.");
            foreach (var id in tended.Swept)
            {
                Console.WriteLine($"  Session {id} closed: it never began.");
            }

            foreach (var id in tended.TimedOut)
            {
                Console.WriteLine($"  Session {id} ended: its player let their time to answer run out.");
            }

            foreach (var id in tended.Abandoned)
            {
                Console.WriteLine($"  Session {id} ended: nobody took the seat its player left.");
            }

            foreach (var id in tended.Released)
            {
                Console.WriteLine($"  Account {id}: suspended a day ago, its seats in unfinished games offered to other players.");
            }

            foreach (var id in tended.Warned)
            {
                Console.WriteLine($"  Session {id}: its player was told a quarter of their time to answer is left.");
            }

            return 0;
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            // The job runs again within the hour, and nothing it does is lost by waiting for it.
            await Console.Error.WriteLineAsync($"The tables could not be tended: {failure.Message}");
            return 1;
        }
    }

    private static async Task<SuspendedSeats> OpenSuspendedAsync(string target, LobbyApi lobby)
    {
        var accounts = await ArtifactStores.OpenAccountsAsync(target);
        return new SuspendedSeats(accounts, lobby);
    }

    /// <summary>
    /// The tables the sweep closed, said through the relay (ADR 0165): it closes them through their records without
    /// holding them, so no change of theirs is raised here, and a process that holds one, or a lobby that lists it,
    /// would otherwise go on showing it open.
    /// </summary>
    internal static void Announce(IChangeRelay relay, Tended tended)
    {
        ArgumentNullException.ThrowIfNull(relay);
        ArgumentNullException.ThrowIfNull(tended);
        foreach (var id in tended.Swept.Concat(tended.TimedOut).Concat(tended.Abandoned))
        {
            relay.Table(id);
        }

        if (tended.Swept.Count > 0 || tended.TimedOut.Count > 0 || tended.Abandoned.Count > 0)
        {
            relay.Lobby();
        }
    }

    /// <summary>
    /// A pass, kept as a run of the log (docs/tabletop/admin-plan.md, A3): when it started and ended, what started it,
    /// and what it did, or the error it stopped on, which is then thrown again. The hourly job's and an operator's
    /// alike. The pass says what it has done as it goes, so a run that stops partway keeps what its earlier stages did:
    /// those tables are closed for good, and the next pass will not find them again. A log that cannot be written to
    /// says so on the console and loses that one run, never the pass.
    /// </summary>
    internal static async Task<(Tended Tended, TendRun Run)> RecordedAsync(ITendLog? log, TendTrigger trigger, string? by, TimeProvider clock, Func<DateTimeOffset, Action<Tended>, Task<Tended>> pass)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(pass);
        var started = clock.GetUtcNow();
        var id = TendRun.NewId(started);
        var done = new Tended([], 0);
        Tended tended;
        try
        {
            tended = await pass(started, progress => done = progress);
        }
        catch (Exception failure)
        {
            await KeepAsync(log, Run(id, started, clock.GetUtcNow(), trigger, by, done, failure.Message));
            throw;
        }

        var run = Run(id, started, clock.GetUtcNow(), trigger, by, tended, error: null);
        await KeepAsync(log, run);
        return (tended, run);
    }

    private static TendRun Run(string id, DateTimeOffset started, DateTimeOffset finished, TendTrigger trigger, string? by, Tended done, string? error) =>
        new(id, started, finished, trigger, by, error is null ? TendOutcome.Completed : TendOutcome.Failed, error, done.Swept, done.Expired, done.TimedOut, done.Abandoned, done.Warned);

    /// <summary>The tend log, or none when it cannot be opened: said on the console, and the pass goes on without it.</summary>
    internal static async Task<ITendLog?> OpenLogAsync(Func<Task<ITendLog>> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        try
        {
            return await open();
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            await Console.Error.WriteLineAsync($"The tending runs could not be opened, so this one is not kept: {failure.Message}");
            return null;
        }
    }

    private static async Task KeepAsync(ITendLog? log, TendRun run)
    {
        if (log is null)
        {
            return;
        }

        try
        {
            await log.RecordAsync(run);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure))
        {
            await Console.Error.WriteLineAsync($"The tending run {run.Id} could not be kept: {failure.Message}");
        }
    }

    /// <summary>One pass: the sweep, the invitations that ran out, the games whose player's time ran out, the games whose open seat nobody took, then the players whose time reaches its last quarter.</summary>
    /// <param name="composer">What reads a table's last move; none, and no game is timed out.</param>
    /// <param name="progress">Told what the pass has done after each stage, so a pass that stops partway is known by what it did.</param>
    /// <param name="suspended">The suspended accounts' seats (ADR 0178); none on a host that signs nobody in.</param>
    internal static async Task<Tended> TendAsync(TableRegistry registry, LobbyApi lobby, DateTimeOffset now, TableComposer? composer = null, TimeProvider? clock = null, Action<Tended>? progress = null, SuspendedSeats? suspended = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(lobby);
        var done = new Tended(await registry.SweepNeverBegunAsync(now), 0);
        progress?.Invoke(done);
        done = done with { Expired = await lobby.ExpireInvitationsNowAsync() };
        progress?.Invoke(done);
        if (suspended is not null)
        {
            done = done with { Released = await suspended.ReleaseAsync(now) };
            progress?.Invoke(done);
        }

        if (composer is not null)
        {
            done = done with { TimedOut = await TurnTimeouts.EndAsync(registry, composer, now, clock ?? TimeProvider.System) };
            progress?.Invoke(done);
            done = done with { Abandoned = await AbandonedSeats.EndAsync(registry, composer, now, clock ?? TimeProvider.System) };
            progress?.Invoke(done);
            done = done with { Warned = await TurnTimeouts.WarnAsync(registry, composer, now, Interval) };
            progress?.Invoke(done);
        }

        return done;
    }
}
