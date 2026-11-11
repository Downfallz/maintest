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
/// hours ago, and an invitation nobody answered in a day is let go of. The tables it picks up to do so take no bot
/// turn here: that is the worker's.
/// </summary>
internal static class TableTend
{
    /// <summary>What one pass did.</summary>
    internal sealed record Tended(IReadOnlyList<string> Swept, int Expired);

    public static async Task<int> RunAsync(IServiceProvider services, CliOptions options, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        var clock = services.GetRequiredService<TimeProvider>();
        var target = options.Record ?? TableHost.DefaultRunsDirectory;
        try
        {
            var store = await ArtifactStores.OpenAsync(target);
            var index = await ArtifactStores.OpenIndexAsync(target);
            var composer = new TableComposer(services, rules, store)
            {
                Contents = await TableHost.OpenContentsAsync(target, CancellationToken.None),
                Turns = new NoAgentTurns(),
                Index = index,
                Basis = ReplayBasis.Of(EngineVersion.Current, services.GetRequiredService<IGameResources>(), rules),
            };
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

            var tended = await TendAsync(registry, lobby, clock.GetUtcNow());
            if (relay is not null)
            {
                await Task.WhenAny(relay.SentAsync(), Task.Delay(TimeSpan.FromSeconds(10), clock));
            }

            Console.WriteLine($"Tended '{target}': {tended.Swept.Count} table{(tended.Swept.Count == 1 ? string.Empty : "s")} that never began closed, {tended.Expired} invitation{(tended.Expired == 1 ? string.Empty : "s")} that ran out let go of.");
            foreach (var id in tended.Swept)
            {
                Console.WriteLine($"  Session {id} closed: it never began.");
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

    /// <summary>One pass: the sweep, then the invitations that ran out.</summary>
    internal static async Task<Tended> TendAsync(TableRegistry registry, LobbyApi lobby, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(lobby);
        var swept = await registry.SweepAsync(now);
        var expired = await lobby.ExpireInvitationsNowAsync();
        return new Tended(swept, expired);
    }
}
