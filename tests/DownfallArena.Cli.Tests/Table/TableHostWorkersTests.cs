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

    /// <summary>
    /// The notices worker's process holds a copy of the table it has not looked at for longer than its watch looks
    /// (<see cref="TableWatch.WatchedFor" />), while the match moved on in the API (#1134): the turn that came due is the
    /// later asking, and the copy is brought up to date before it is read, so the seat's account is told.
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
        var first = await AskedAsync(table, after: 0);
        await table.RecordAsync(Cancel);
        using var worker = hosted.Registry();
        var stale = (await worker.FindAsync(table.Id, Cancel)).Table.ShouldNotBeNull();
        stale.Touch(DateTimeOffset.UtcNow - TableWatch.WatchedFor - TimeSpan.FromMinutes(1));
        foreach (var seat in table.Seats)
        {
            var asked = seat.Person!.Waiting.ShouldNotBeNull().Asked;
            var pass = System.Text.Json.JsonSerializer.Serialize(new { kind = "Evolution", pass = true, asked });
            (await table.Api.HandleAsync("POST", $"/api/seat/{seat.Name}/decision", pass, seat.Token)).Status.ShouldBe(204);
        }

        var later = await AskedAsync(table, after: first);
        var channels = new InMemoryWorkChannels(TimeProvider.System);
        var notices = await channels.OpenAsync(QueuedNotice.Queue, Cancel);
        var sender = new RecordedPush();
        var devices = new Application.Accounts.AccountDevices(new InMemoryDeviceStore(), TimeProvider.System);
        await devices.AddAsync(account, new Application.Accounts.PushDevice("https://push.example/phone", InMemoryDeviceStore.Key, InMemoryDeviceStore.Auth, default), Cancel);
        var push = new PushNotices(sender, "BP8", TimeProvider.System) { Accounts = new AccountNotifier(log: null, new AccountNotices(sender, devices)) };
        using var stopping = new CancellationTokenSource();

        var noticing = await TableHost.StartNoticesAsync(channels, notices, push, worker, TimeProvider.System, stopping.Token);
        await notices.SendAsync(QueuedNotice.Of(new DueTurn(table.Id, table.Seats[0].Name, later, 2)).ToJson(), cancellationToken: Cancel);
        for (var attempt = 0; attempt < 250 && sender.Sent.Count == 0; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        await stopping.CancelAsync();
        await noticing.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        sender.Sent.ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example/phone");
        table.Dispose();
    }

    /// <summary>Waits for the table's first seat to be asked something later than <paramref name="after" />, and answers the asking.</summary>
    private static async Task<long> AskedAsync(PlayedTable table, long after)
    {
        for (var attempt = 0; attempt < 300 && table.Seats.Any(seat => seat.Person!.Waiting is not { } question || question.Asked <= after); attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        return table.Seats[0].Person!.Waiting.ShouldNotBeNull().Asked;
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
