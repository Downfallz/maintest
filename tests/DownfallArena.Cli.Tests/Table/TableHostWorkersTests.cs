using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The workers of the queued turns as the table host starts them (ADR 0159, phase 5): none when the turns are taken
/// in the process, both until the host stops otherwise, and the turns taken in the process when a queue cannot be
/// opened.
/// </summary>
public sealed class TableHostWorkersTests : IDisposable
{
    private readonly HostedTables _hosted = new();

    public void Dispose() => _hosted.Dispose();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task No_worker_runs_when_the_turns_are_taken_in_the_process()
    {
        using var registry = _hosted.Registry();

        var working = await TableHost.StartWorkersAsync(null, null, registry, TimeProvider.System, Cancel);

        working.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Both_workers_run_until_the_host_stops()
    {
        using var registry = _hosted.Registry();
        var channels = new InMemoryWorkChannels(TimeProvider.System);
        var turns = await channels.OpenAsync(AgentTurnWorker.Queue, Cancel);
        using var stopping = new CancellationTokenSource();

        var working = await TableHost.StartWorkersAsync(channels, turns, registry, TimeProvider.System, stopping.Token);
        var runningBeforeTheStop = !working.IsCompleted;
        await stopping.CancelAsync();
        await working.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        runningBeforeTheStop.ShouldBeTrue();
    }

    [Fact]
    public async Task A_queue_that_cannot_be_opened_leaves_the_turns_in_the_process()
    {
        var turns = await TableHost.OpenChannelAsync(new Unopenable(), AgentTurnWorker.Queue, Cancel);

        turns.ShouldBeNull();
    }

    [Fact]
    public async Task The_notices_worker_the_host_starts_sends_what_the_door_put_on_the_queue()
    {
        using var registry = _hosted.Registry();
        var channels = new InMemoryWorkChannels(TimeProvider.System);
        var notices = await channels.OpenAsync(QueuedNotice.Queue, Cancel);
        var sender = new RecordedPush();
        var account = SharedKernel.Identifiers.AccountId.New();
        var devices = new Application.Accounts.AccountDevices(new InMemoryDeviceStore(), TimeProvider.System);
        await devices.AddAsync(account, new Application.Accounts.PushDevice("https://push.example/phone", InMemoryDeviceStore.Key, InMemoryDeviceStore.Auth, default), Cancel);
        var door = new AccountNotifier(log: null, new AccountNotices(sender, devices)) { Outbox = notices };
        var push = new PushNotices(sender, "BP8", TimeProvider.System) { Accounts = door, Outbox = notices };
        using var stopping = new CancellationTokenSource();

        var noticing = await TableHost.StartNoticesAsync(channels, notices, push, registry, TimeProvider.System, stopping.Token);
        await door.NotifyAsync(account, new Application.Accounts.Notice(Application.Accounts.NoticeKind.MatchOver, "Victory", "You won.", "/lobby", "match:t1"), Cancel);
        for (var attempt = 0; attempt < 250 && sender.Sent.Count == 0; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        await stopping.CancelAsync();
        await noticing.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        sender.Sent.ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example/phone");
    }

    [Fact]
    public async Task Without_a_queue_or_push_keys_no_notices_worker_runs()
    {
        using var registry = _hosted.Registry();

        var noticing = await TableHost.StartNoticesAsync(null, null, null, registry, TimeProvider.System, Cancel);

        noticing.IsCompleted.ShouldBeTrue();
    }

    private sealed class Unopenable : IWorkChannels
    {
        public Task<IWorkChannel> OpenAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromException<IWorkChannel>(new IOException("The account refuses this identity its queues."));
    }
}
