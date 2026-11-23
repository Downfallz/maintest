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
    private readonly RecordedPush _sender = new();

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

    private AccountNoticeLog Log => new(_notices, TimeProvider.System);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>Both seats persons played to the end by their understudies, whose turns the worker takes.</summary>
    private static TableRequest TwoPeople => new(null, null, Who: null, Handover: 999, Seed: 7);

    private TableWorker.Over Over(bool devices) => new(new FileArtifactStore(_hosted.RunsDirectory), _hosted.Index, _channels)
    {
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

    /// <summary>A match between the two accounts, recorded by the host and played to its end by the worker, until it is.</summary>
    private async Task WorkedToItsEndAsync(AccountId first, AccountId second, IPushSender? sender, bool devices)
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
        var working = TableWorker.WorkAsync(_hosted.Services, HostedTables.Rules, Over(devices), sender, stopping.Token);
        var episodes = Path.Combine(_hosted.RunsDirectory, table.Id, RunRecorder.EpisodesFile);
        for (var waited = 0; waited < 600 && (await Log.ListAsync(first, Cancel)).Count == 0; waited++)
        {
            await Task.Delay(100, Cancel);
        }

        // The pushes leave the worker after the bell's write: a moment for those still on their way.
        await Task.Delay(sender is null ? 0 : 500, Cancel);
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

        await WorkedToItsEndAsync(mark, bastien, _sender, devices: true);

        _sender.Sent.Select(sent => sent.Endpoint).ShouldBe(["https://push.example/bastien"]);
        (await Log.ListAsync(mark, Cancel)).ShouldHaveSingleItem().Kind.ShouldBe(NoticeKind.MatchOver);
    }

    [Fact]
    public async Task With_push_keys_the_worker_puts_its_pushes_on_the_notices_queue_and_sends_them_from_there()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");

        await WorkedToItsEndAsync(mark, bastien, _sender, devices: true);

        _channels.SentOn(QueuedNotice.Queue).ShouldBe(2);
        _sender.Sent.Select(sent => sent.Endpoint).ShouldBe(["https://push.example/MarkF", "https://push.example/bastien"], ignoreOrder: true);
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
