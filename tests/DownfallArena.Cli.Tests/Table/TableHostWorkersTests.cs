using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;
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

        working.Running.IsCompleted.ShouldBeTrue();
        working.Turns.ShouldBe(TurnsTaken.InProcess);
    }

    [Fact]
    public async Task Both_workers_run_until_the_host_stops()
    {
        using var registry = _hosted.Registry();
        var channels = new InMemoryWorkChannels(TimeProvider.System);
        var turns = await channels.OpenAsync(AgentTurnWorker.Queue, Cancel);
        using var stopping = new CancellationTokenSource();

        var working = await TableHost.StartWorkersAsync(channels, turns, registry, TimeProvider.System, stopping.Token);
        var runningBeforeTheStop = !working.Running.IsCompleted;
        await stopping.CancelAsync();
        await working.Running.WaitAsync(Waits.Within, Cancel);

        runningBeforeTheStop.ShouldBeTrue();
        working.Turns.ShouldBe(TurnsTaken.ByThisHost);
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
        await sender.FirstSent.WaitAsync(Waits.Within, Cancel);

        await stopping.CancelAsync();
        await noticing.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        sender.Sent.ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example/phone");
    }

    /// <summary>
    /// The notices worker's process holds a copy of the table it has not looked at for longer than its watch looks
    /// (<see cref="TableWatch.WatchedFor" />), while the match moved on in the API (#1134): the turn that came due is the
    /// later asking, and the copy is brought up to date before it is read, so the seat's account is told. The worker is
    /// a host of its own over the same runs and index, as the two processes are: a registry over the API's own services
    /// would rebuild the match into the API's match repository, under the API's running match (#1187).
    /// </summary>
    [Fact]
    public async Task A_turn_due_at_a_table_this_process_holds_stale_is_told_once_the_table_is_brought_up_to_date()
    {
        using var hosted = new HostedTables(recording: true);
        using var api = hosted.Registry();
        var table = await hosted.Composer.ComposeAsync(HostedTables.TwoPeople, Cancel);
        using (var place = (await api.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await api.AddAsync(table, place, Cancel);
        }

        var account = SharedKernel.Identifiers.AccountId.New();
        table.Seats[0].Account = account;
        foreach (var seat in table.Seats)
        {
            seat.Person!.Arrive();
        }

        table.Session.Begin();
        var first = await table.Seats.AllAskedAsync(after: 0, Cancel);
        await table.RecordAsync(Cancel);
        using var workerHost = new HostedTables(_ => new FileArtifactStore(hosted.RunsDirectory), index: hosted.Index);
        using var worker = workerHost.Registry();
        var found = await worker.FindAsync(table.Id, Cancel);
        found.Unavailable.ShouldBeNull();
        var stale = found.Table.ShouldNotBeNull();
        stale.Touch(DateTimeOffset.UtcNow - TableWatch.WatchedFor - TimeSpan.FromMinutes(1));
        foreach (var seat in table.Seats)
        {
            var asked = seat.Person!.Waiting.ShouldNotBeNull().Asked;
            var pass = System.Text.Json.JsonSerializer.Serialize(new { kind = "Evolution", pass = true, asked });
            (await table.Api.HandleAsync("POST", $"/api/seat/{seat.Name}/decision", pass, seat.Token)).Status.ShouldBe(204);
        }

        var later = await table.Seats.AllAskedAsync(after: first, Cancel);
        var channels = new InMemoryWorkChannels(TimeProvider.System);
        var notices = await channels.OpenAsync(QueuedNotice.Queue, Cancel);
        var sender = new RecordedPush();
        var devices = new Application.Accounts.AccountDevices(new InMemoryDeviceStore(), TimeProvider.System);
        await devices.AddAsync(account, new Application.Accounts.PushDevice("https://push.example/phone", InMemoryDeviceStore.Key, InMemoryDeviceStore.Auth, default), Cancel);
        var push = new PushNotices(sender, "BP8", TimeProvider.System) { Accounts = new AccountNotifier(log: null, new AccountNotices(sender, devices)) };
        using var stopping = new CancellationTokenSource();

        var noticing = await TableHost.StartNoticesAsync(channels, notices, push, worker, TimeProvider.System, stopping.Token);
        await notices.SendAsync(QueuedNotice.Of(new DueTurn(table.Id, table.Seats[0].Name, later, 2)).ToJson(), cancellationToken: Cancel);
        await sender.FirstSent.WaitAsync(Waits.Within, Cancel);

        await stopping.CancelAsync();
        await noticing.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        sender.Sent.ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example/phone");
        table.Dispose();
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
