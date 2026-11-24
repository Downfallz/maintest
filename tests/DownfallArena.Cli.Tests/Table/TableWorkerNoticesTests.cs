using DownfallArena.Application.Accounts;
using DownfallArena.Application.Learning.Ports;
using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Work;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The worker's door (#1109): <see cref="TableWorker.WorkAsync" /> builds the door every notice goes through (ADR 0201)
/// for a match its turn ends. Every other test builds it by hand; here the worker builds it from what it works over,
/// and what reaches a player is seen.
/// </summary>
public sealed class TableWorkerNoticesTests : IDisposable
{
    private readonly SentWork _channels = new(new InMemoryWorkChannels(TimeProvider.System));
    private readonly HostedTables _hosted;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"downfall-worker-notices-{Guid.NewGuid():N}");
    private readonly FileAccountStore _accounts;
    private readonly FileNoticeStore _notices;
    private readonly FileDeviceStore _devices;
    private readonly FilePreferenceStore _preferences;
    // A push service that answers in a second: a run under load sends no faster than this (#1162).
    private readonly RecordedPush _sender = new() { Lag = TimeSpan.FromSeconds(1) };

    public TableWorkerNoticesTests()
    {
        var queue = _channels.OpenAsync(AgentTurnWorker.Queue, CancellationToken.None).GetAwaiter().GetResult();
        _hosted = new HostedTables(directory => new FileArtifactStore(directory), turns: new QueuedAgentTurns(queue, TimeProvider.System));
        _accounts = new FileAccountStore(Path.Combine(_root, "accounts"));
        _notices = new FileNoticeStore(Path.Combine(_root, "notices"));
        _devices = new FileDeviceStore(Path.Combine(_root, "devices"));
        _preferences = new FilePreferenceStore(Path.Combine(_root, "preferences"));
    }

    public void Dispose()
    {
        _hosted.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Notice News => new(NoticeKind.FriendAccepted, "A new friend", "bastien accepted your friend request.", "/lobby#friends", "friend:test");

    private AccountNoticeLog Log => new(_notices, TimeProvider.System);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>Both seats persons played to the end by their understudies, whose turns the worker takes.</summary>
    private static TableRequest TwoPeople => new(null, null, Who: null, Handover: 999, Seed: 7);

    private TableWorker.Over Over(bool devices, IChangeRelay? relay) => new(new FileArtifactStore(_hosted.RunsDirectory), _hosted.Index, _channels)
    {
        Relay = relay,
        Accounts = _accounts,
        Notices = _notices,
        Devices = devices ? _devices : null,
        Preferences = _preferences,
    };

    private async Task<AccountId> PlayerAsync(string name)
    {
        var account = (await new AccountRegistry(_accounts, TimeProvider.System).RegisterAsync(new ExternalIdentity(ExternalIdentity.Development, name), name, Cancel)).Value;
        await new AccountDevices(_devices, TimeProvider.System).AddAsync(account.Id, new PushDevice($"https://push.example/{name}", InMemoryDeviceStore.Key, InMemoryDeviceStore.Auth, default), Cancel);
        return account.Id;
    }

    /// <summary>
    /// A match between the two accounts, recorded by the host and played to its end by the worker, until it is; with a
    /// sender, until the pushes it put on the notices queue are sent, as many as <paramref name="pushes" /> at least.
    /// </summary>
    private async Task WorkedToItsEndAsync(AccountId first, AccountId second, RecordedPush? sender, bool devices, int pushes = 0)
    {
        using var host = _hosted.Registry();
        var table = await _hosted.Composer.ComposeAsync(TwoPeople, Cancel);
        using (var place = (await host.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await host.AddAsync(table, place, Cancel);
        }

        table.Seats[0].Account = first;
        table.Seats[1].Account = second;
        foreach (var seat in table.Seats)
        {
            seat.Person!.Arrive();
        }

        table.Session.Begin();
        await table.RecordAsync(Cancel);
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var working = TableWorker.WorkAsync(_hosted.Services, HostedTables.Rules, Over(devices, relay: null), sender, stopping.Token);
        var episodes = Path.Combine(_hosted.RunsDirectory, table.Id, RunRecorder.EpisodesFile);
        await Waits.UntilAsync(async () => (await Log.ListAsync(first, Cancel)).Count > 0, "the bell holds an entry", Cancel);

        // The pushes leave the worker after the bell's write, and are sent from the queue after that: waited for until
        // every one put on the queue is sent, never for a time (#1162).
        await Waits.UntilAsync(() => sender is null || (sender.Count >= pushes && sender.Count >= _channels.SentOn(QueuedNotice.Queue)), "every push put on the queue was sent", Cancel);

        await stopping.CancelAsync();
        await working;
        File.Exists(episodes).ShouldBeTrue("the worker played the match to its end");
    }

    [Fact]
    public async Task Without_push_keys_a_match_the_worker_ends_is_kept_in_each_player_s_bell()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");

        await WorkedToItsEndAsync(mark, bastien, sender: null, devices: false);

        foreach (var account in new[] { mark, bastien })
        {
            (await Log.ListAsync(account, Cancel)).ShouldHaveSingleItem().Kind.ShouldBe(NoticeKind.MatchOver);
        }
    }

    [Fact]
    public async Task A_seat_whose_account_is_gone_is_kept_in_no_bell_by_the_worker()
    {
        var mark = await PlayerAsync("MarkF");
        var gone = AccountId.New();

        await WorkedToItsEndAsync(mark, gone, sender: null, devices: false);

        (await Log.ListAsync(gone, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_result_an_account_turned_off_is_kept_in_its_bell_and_not_pushed_by_the_worker()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await _preferences.SaveNoticesAsync(mark, NoticePreferences.Default with { Results = false }, Cancel);

        await WorkedToItsEndAsync(mark, bastien, _sender, devices: true, pushes: 1);

        _sender.Sent.Select(sent => sent.Endpoint).ShouldBe(["https://push.example/bastien"]);
        (await Log.ListAsync(mark, Cancel)).ShouldHaveSingleItem().Kind.ShouldBe(NoticeKind.MatchOver);
    }

    [Fact]
    public async Task With_push_keys_the_worker_puts_its_pushes_on_the_notices_queue_and_sends_them_from_there()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");

        await WorkedToItsEndAsync(mark, bastien, _sender, devices: true, pushes: 2);

        _channels.SentOn(QueuedNotice.Queue).ShouldBe(2);
        _sender.Sent.Select(sent => sent.Endpoint).ShouldBe(["https://push.example/MarkF", "https://push.example/bastien"], ignoreOrder: true);
    }

    [Fact]
    public async Task With_a_relay_a_notice_the_worker_s_door_keeps_is_said_to_the_account_s_pages_through_it()
    {
        var mark = await PlayerAsync("MarkF");
        var relay = new RecordedRelay();
        var worked = await TableWorker.ComposeAsync(_hosted.Services, HostedTables.Rules, Over(devices: false, relay), sender: null, Cancel);

        await worked.Composer.Notifier.ShouldNotBeNull().NotifyAsync(mark, News, Cancel);

        relay.Said.ShouldBe([$"account {mark.Value}"]);
    }

    /// <summary>The queues, counting what is put on each.</summary>
    private sealed class SentWork(IWorkChannels inner) : IWorkChannels
    {
        private readonly Dictionary<string, int> _sent = [];

        public int SentOn(string name)
        {
            lock (_sent)
            {
                return _sent.GetValueOrDefault(name);
            }
        }

        public async Task<IWorkChannel> OpenAsync(string name, CancellationToken cancellationToken = default) =>
            new Counted(await inner.OpenAsync(name, cancellationToken), this);

        private void Sent(string name)
        {
            lock (_sent)
            {
                _sent[name] = _sent.GetValueOrDefault(name) + 1;
            }
        }

        private sealed class Counted(IWorkChannel inner, SentWork counts) : IWorkChannel
        {
            public string Name => inner.Name;

            public Task<QueuedWork> SendAsync(string body, TimeSpan? keptFor = null, TimeSpan? hiddenFor = null, CancellationToken cancellationToken = default)
            {
                counts.Sent(inner.Name);
                return inner.SendAsync(body, keptFor, hiddenFor, cancellationToken);
            }

            public Task<IReadOnlyList<QueuedWork>> ReceiveAsync(int most, TimeSpan hiddenFor, CancellationToken cancellationToken = default) => inner.ReceiveAsync(most, hiddenFor, cancellationToken);

            public Task DeleteAsync(QueuedWork work, CancellationToken cancellationToken = default) => inner.DeleteAsync(work, cancellationToken);

            public Task RetryAsync(QueuedWork work, TimeSpan after, CancellationToken cancellationToken = default) => inner.RetryAsync(work, after, cancellationToken);

            public Task<IReadOnlyList<PeekedWork>> PeekAsync(int most, CancellationToken cancellationToken = default) => inner.PeekAsync(most, cancellationToken);

            public Task<int> CountAsync(CancellationToken cancellationToken = default) => inner.CountAsync(cancellationToken);
        }
    }
}
