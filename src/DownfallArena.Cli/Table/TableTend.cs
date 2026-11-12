using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Application.Learning;
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

            var tended = await TendAsync(registry, lobby, clock.GetUtcNow(), composer, clock);
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

    /// <summary>One pass: the sweep, the invitations that ran out, the games whose player's time ran out, the games whose open seat nobody took, then the players whose time reaches its last quarter.</summary>
    /// <param name="composer">What reads a table's last move; none, and no game is timed out.</param>
    internal static async Task<Tended> TendAsync(TableRegistry registry, LobbyApi lobby, DateTimeOffset now, TableComposer? composer = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(lobby);
        var swept = await registry.SweepAsync(now);
        var expired = await lobby.ExpireInvitationsNowAsync();
        var timedOut = composer is null ? [] : await TurnTimeouts.EndAsync(registry, composer, now, clock ?? TimeProvider.System);
        var abandoned = composer is null ? [] : await AbandonedSeats.EndAsync(registry, composer, now, clock ?? TimeProvider.System);
        var warned = composer is null ? [] : await TurnTimeouts.WarnAsync(registry, composer, now, Interval);
        return new Tended(swept, expired) { TimedOut = timedOut, Warned = warned, Abandoned = abandoned };
    }
}
