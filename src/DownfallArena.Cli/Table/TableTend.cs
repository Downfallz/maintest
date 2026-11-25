using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Application.FeedbackBox;
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
/// ends with that seat giving it up (ADR 0170), and an account's notice past 90 days is erased from its bell at rest
/// (ADR 0201, #1118). The tables it picks up to do so take no bot turn here: that is the worker's.
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

        /// <summary>How much closed feedback was let go of, 180 days after it was closed (ADR 0189).</summary>
        public int FeedbackLetGo { get; init; }

        /// <summary>How many accounts' notice logs let go of what was past 90 days (ADR 0201, #1118).</summary>
        public int NoticesErased { get; init; }

        /// <summary>How many guests no browser could reach any more were removed (ADR 0207).</summary>
        public int GuestsRemoved { get; init; }
    }

    /// <summary>How often the job runs (infra/main.bicep): the warning is sent by the one pass of that interval that finds a quarter left.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <param name="said">Where the pass's lines go: the console unless a test reads them (#756). Failures stay on the console's error stream.</param>
    public static async Task<int> RunAsync(IServiceProvider services, CliOptions options, RuleSet rules, Action<string>? said = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        said ??= Console.WriteLine;
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
            using var tending = await ComposeAsync(services, options, rules, sender, target, WebPubSubRelay.FromEnvironment());
            var relay = tending.Relay;

            // Every run is kept for the admin panel's Jobs tab (A3), the one that failed too. A log that cannot be
            // opened costs the run its record, never the pass: the tables are tended all the same.
            var log = await OpenLogAsync(() => ArtifactStores.OpenTendLogAsync(target));
            var (tended, _) = await RecordedAsync(log, TendTrigger.Schedule, by: null, clock, (now, progress) => TendAsync(tending.Chores, now, progress));
            if (relay is not null)
            {
                Announce(relay, tended);
                await Task.WhenAny(relay.SentAsync(), Task.Delay(TimeSpan.FromSeconds(10), clock));
            }

            Say(said, target, tended);
            return 0;
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            // The job runs again within the hour, and nothing it does is lost by waiting for it.
            await Console.Error.WriteLineAsync($"The tables could not be tended: {failure.Message}");
            return 1;
        }
    }

    /// <summary>What one pass works with, composed as the job composes it, not yet run: the tables, the lobby and the door every notice goes through (ADR 0201).</summary>
    internal sealed record Tending(TableRegistry Registry, LobbyApi Lobby, TableComposer Composer, IChangeRelay? Relay, TimeProvider Clock) : IDisposable
    {
        /// <summary>The suspended accounts' seats (ADR 0178); none on a host that signs nobody in.</summary>
        public SuspendedSeats? Suspended { get; init; }

        /// <summary>What lets go of feedback closed 180 days ago (ADR 0189); none where it cannot be opened.</summary>
        public LetGoOfClosedFeedback? Feedback { get; init; }

        public FeedbackIndex? FeedbackStore { get; init; }

        /// <summary>The accounts' notice logs (ADR 0201), the bell's news kept whether or not the job can push; none on a host that signs nobody in.</summary>
        public AccountNoticeLog? Notices { get; init; }

        /// <summary>The guests no browser can reach any more (ADR 0207); none on a host that signs nobody in.</summary>
        public IdleGuests? Guests { get; init; }

        /// <summary>What the job's pass tends, every stage of it.</summary>
        public Chores Chores => new(Registry, Lobby, Clock)
        {
            Composer = Composer,
            Suspended = Suspended,
            Feedback = Feedback,
            FeedbackIndex = FeedbackStore,
            Notices = Notices,
            Guests = Guests,
        };

        public void Dispose() => Registry.Dispose();
    }

    /// <summary>
    /// The pass's parts over <paramref name="target" />, wired as the job runs them: what <see cref="RunAsync" /> tends
    /// with, and what the tests compose to see the door is built and handed to the lobby (#1109).
    /// </summary>
    /// <param name="sender">What sends a notice to a device; none on a job without push keys, whose bell still keeps the news.</param>
    /// <param name="relay">Where what the pass moves is said (ADR 0165): the one the environment names, a recording one in the tests (#1153); none.</param>
    internal static async Task<Tending> ComposeAsync(IServiceProvider services, CliOptions options, RuleSet rules, IPushSender? sender, string target, IChangeRelay? relay = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        var clock = services.GetRequiredService<TimeProvider>();
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
        // The bell keeps the news whether or not the host can push (ADR 0201).
        if (options.Accounts != AccountsMode.Off)
        {
            var accounts = await ArtifactStores.OpenAccountsAsync(target);
            var noticeLog = new AccountNoticeLog(await ArtifactStores.OpenNoticesAsync(target), clock);
            composer.Notifier = new AccountNotifier(noticeLog, sender is null ? null : new AccountNotices(sender, new AccountDevices(await ArtifactStores.OpenDevicesAsync(target), clock)))
            {
                Exists = async (id, token) => await accounts.FindAsync(id, token) is not null,
                Preferences = await ArtifactStores.OpenPreferencesAsync(target),
            };
            composer.NameOf = async id => (await accounts.FindAsync(id))?.Username.Value;
        }

        // Whether two players are still friends is what lets an invitation between them stand (ADR 0129).
        var friends = options.Accounts == AccountsMode.Off
            ? null
            : new Friends(await ArtifactStores.OpenFriendsAsync(target), await ArtifactStores.OpenAccountsAsync(target), clock);
        // The accounts suspended a day ago (ADR 0178), whose seats are offered to other players.
        var suspendedAccounts = options.Accounts == AccountsMode.Off ? null : await ArtifactStores.OpenAccountsAsync(target);
        // The guests no browser can reach any more (ADR 0207), removed with what was kept for them.
        var guests = suspendedAccounts is null
            ? null
            : new IdleGuests(suspendedAccounts, await ArtifactStores.OpenActivityAsync(target), await ArtifactStores.OpenPreferencesAsync(target), clock);

        // Feedback closed 180 days ago is let go of (ADR 0189); a store that cannot be opened costs that alone.
        // Through the index's store (#866), so what is let go of leaves the index too.
        var feedbackStore = await TableHost.OpenFeedbackAsync(target, clock, CancellationToken.None);
        var feedback = feedbackStore is null ? null : new LetGoOfClosedFeedback(feedbackStore.Records, clock);

        // What the pass closes and lets go of is said to the pages and the API through the relay, when there is one
        // (ADR 0165), and the job waits for it to have gone before it exits.
        composer.Relay = relay;
        composer.Lobby = relay is null ? null : new LobbyChanges { Relay = relay };
        composer.Notifier?.Changes = composer.Lobby;

        // Made last, once nothing is left to open: nothing can fail between it and the pass that lets go of it.
        var registry = new TableRegistry(index) { Loader = new TableLoader(new StoredSessions(store), composer, clock), Basis = composer.Basis };
        var lobby = new LobbyApi(registry, composer, clock) { Friends = friends, Changes = composer.Lobby, Notifier = composer.Notifier };
        var suspended = suspendedAccounts is null ? null : new SuspendedSeats(suspendedAccounts, lobby);

        return new Tending(registry, lobby, composer, relay, clock) { Suspended = suspended, Feedback = feedback, FeedbackStore = feedbackStore, Notices = composer.Notifier?.Log, Guests = guests };
    }

    /// <summary>What a pass did, a line for the pass and one for each table, account or feedback it touched.</summary>
    private static void Say(Action<string> said, string target, Tended tended)
    {
        said($"Tended '{target}': {tended.Swept.Count} table{(tended.Swept.Count == 1 ? string.Empty : "s")} that never began closed, {tended.Expired} invitation{(tended.Expired == 1 ? string.Empty : "s")} that ran out let go of, {tended.TimedOut.Count} game{(tended.TimedOut.Count == 1 ? string.Empty : "s")} timed out, {tended.Abandoned.Count} left by a player ended.");
        foreach (var id in tended.Swept)
        {
            said($"  Session {id} closed: it never began.");
        }

        foreach (var id in tended.TimedOut)
        {
            said($"  Session {id} ended: its player let their time to answer run out.");
        }

        foreach (var id in tended.Abandoned)
        {
            said($"  Session {id} ended: nobody took the seat its player left.");
        }

        foreach (var id in tended.Released)
        {
            said($"  Account {id}: suspended a day ago, its seats in unfinished games offered to other players.");
        }

        if (tended.NoticesErased > 0)
        {
            said($"  Notices: the bells of {tended.NoticesErased} account{(tended.NoticesErased == 1 ? string.Empty : "s")} let go of what was older than {AccountNoticeLog.KeptFor.TotalDays:0} days.");
        }

        if (tended.GuestsRemoved > 0)
        {
            said($"  Guests: {tended.GuestsRemoved} unused for {IdleGuests.KeptFor.TotalDays:0} days removed.");
        }

        if (tended.FeedbackLetGo > 0)
        {
            said($"  Feedback: {tended.FeedbackLetGo} closed more than {Feedback.KeptClosed.TotalDays:0} days ago let go of.");
        }

        foreach (var id in tended.Warned)
        {
            said($"  Session {id}: its player was told a quarter of their time to answer is left.");
        }
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
        new(id, started, finished, trigger, by, error is null ? TendOutcome.Completed : TendOutcome.Failed, error, done.Swept, done.Expired, done.TimedOut, done.Abandoned, done.Warned)
        {
            GuestsRemoved = done.GuestsRemoved,
            NoticesErased = done.NoticesErased,
            FeedbackLetGo = done.FeedbackLetGo,
        };

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

    /// <summary>
    /// The notices past 90 days erased from every account's log (#1118), a log at a time. A log that cannot be read or
    /// written is a line on the console, and the next pass tries it again. How many logs were written.
    /// </summary>
    internal static async Task<int> EraseNoticesAsync(AccountNoticeLog notices)
    {
        ArgumentNullException.ThrowIfNull(notices);
        IReadOnlyList<SharedKernel.Identifiers.AccountId> accounts;
        try
        {
            accounts = await notices.AccountsAsync();
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
        {
            Console.WriteLine($"  The notice logs could not be listed, so none was swept: {Hosting.HttpHost.OneLine(failure)}");
            return 0;
        }

        var erased = 0;
        foreach (var account in accounts)
        {
            try
            {
                erased += await notices.EraseExpiredAsync(account) ? 1 : 0;
            }
            catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is Azure.Identity.AuthenticationFailedException)
            {
                Console.WriteLine($"  Account {account}: its notice log could not be swept: {Hosting.HttpHost.OneLine(failure)}");
            }
        }

        return erased;
    }

    /// <summary>
    /// What one pass tends: the tables and the lobby, and the parts each later stage needs, a stage left out where its
    /// part is none. The hourly job's (<see cref="Tending.Chores" />), a pass run from the panel, and the tests'.
    /// </summary>
    internal sealed record Chores(TableRegistry Registry, LobbyApi Lobby, TimeProvider Clock)
    {
        /// <summary>What reads a table's last move; none, and no game is timed out, ended or warned.</summary>
        public TableComposer? Composer { get; init; }

        /// <summary>The suspended accounts' seats (ADR 0178); none on a host that signs nobody in.</summary>
        public SuspendedSeats? Suspended { get; init; }

        /// <summary>What lets go of feedback closed 180 days ago (ADR 0189); none on a host that keeps none.</summary>
        public LetGoOfClosedFeedback? Feedback { get; init; }

        /// <summary>The index of feedback, built again from every record (#866); none on a host that keeps none.</summary>
        public FeedbackIndex? FeedbackIndex { get; init; }

        /// <summary>The accounts' notice logs (ADR 0201), swept for what is past 90 days (#1118); none on a host that signs nobody in.</summary>
        public AccountNoticeLog? Notices { get; init; }

        /// <summary>The guests no browser can reach any more (ADR 0207); none on a host that signs nobody in.</summary>
        public IdleGuests? Guests { get; init; }
    }

    /// <summary>One pass: the sweep, the invitations that ran out, the games whose player's time ran out, the games whose open seat nobody took, the players whose time reaches its last quarter, then the feedback closed long enough ago, the notices past 90 days and the guests unused for 31 days.</summary>
    /// <param name="progress">Told what the pass has done after each stage, so a pass that stops partway is known by what it did.</param>
    internal static async Task<Tended> TendAsync(Chores chores, DateTimeOffset now, Action<Tended>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(chores);
        var (registry, lobby, composer, clock) = (chores.Registry, chores.Lobby, chores.Composer, chores.Clock);
        var (suspended, feedback, feedbackIndex, notices) = (chores.Suspended, chores.Feedback, chores.FeedbackIndex, chores.Notices);
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
            done = done with { TimedOut = await TurnTimeouts.EndAsync(registry, composer, now, clock) };
            progress?.Invoke(done);
            done = done with { Abandoned = await AbandonedSeats.EndAsync(registry, composer, now, clock) };
            progress?.Invoke(done);
            done = done with { Warned = await TurnTimeouts.WarnAsync(registry, composer, now, Interval) };
            progress?.Invoke(done);
        }

        if (feedback is not null)
        {
            done = done with { FeedbackLetGo = await feedback.HandleAsync() };
            progress?.Invoke(done);
        }

        // The index of feedback built again from every record (#866): what a revision that keeps none changed is put right.
        if (feedbackIndex is not null && !await feedbackIndex.RebuildAsync())
        {
            Console.WriteLine("  The index of feedback was written meanwhile: the next pass builds it again.");
        }

        // Last, and each log on its own: a store that fails costs the sweep of that log alone, never a stage after it.
        if (notices is not null)
        {
            done = done with { NoticesErased = await EraseNoticesAsync(notices) };
            progress?.Invoke(done);
        }

        // The guests no browser can reach (ADR 0207), last too: a store that fails costs this stage alone.
        if (chores.Guests is { } guests)
        {
            done = done with { GuestsRemoved = await guests.RemoveAsync(now) };
            progress?.Invoke(done);
        }

        return done;
    }
}
