using DownfallArena.Application.Accounts;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;
using DownfallArena.Cli.Table;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// What the notices worker does with what the queue holds (ADR 0203), whatever the queue: a push service out for a
/// moment is tried again, a device it says is gone is forgotten and remembered as gone, and a notice that keeps failing
/// is given up on, onto the poison queue and then for good onto the dead letters. Retries come back at once here, so
/// five deliveries take no minute.
/// </summary>
public abstract class NoticeWorkerContract
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly AccountId _mark = AccountId.New();
    private readonly FailingSender _sender = new();
    private readonly AccountDevices _devices = new(new InMemoryDeviceStore(), TimeProvider.System);

    /// <summary>A fresh, empty queue of this name.</summary>
    protected abstract Task<IWorkChannel> ChannelAsync(string name);

    private const string Phone = "https://push.example/phone";

    [Fact]
    public async Task A_notice_its_push_service_refused_for_a_moment_is_tried_again_and_reaches_the_device()
    {
        var (worker, notices, _) = await WorkerAsync();
        await PhoneAsync();
        _sender.Failures = 1;
        await QueueAsync(notices);

        await worker.RunOnceAsync(Cancel);
        await worker.RunOnceAsync(Cancel);

        _sender.Sent.ShouldHaveSingleItem().ShouldBe(Phone);
        (await _devices.ListAsync(_mark, Cancel)).ShouldHaveSingleItem("a service that is out drops no device");
        (await notices.CountAsync(Cancel)).ShouldBe(0);
    }

    [Fact]
    public async Task A_device_its_push_service_says_is_gone_is_forgotten_remembered_as_gone_and_its_notice_is_done_with()
    {
        var (worker, notices, poison) = await WorkerAsync();
        await PhoneAsync();
        _sender.Gone = true;
        await QueueAsync(notices);

        await worker.RunOnceAsync(Cancel);

        (await _devices.ListAsync(_mark, Cancel)).ShouldBeEmpty();
        (await _devices.IsGoneAsync(_mark, Phone, Cancel)).ShouldBeTrue();
        (await notices.CountAsync(Cancel)).ShouldBe(0);
        (await poison.CountAsync(Cancel)).ShouldBe(0, "a gone device is no failure");
    }

    [Fact]
    public async Task A_notice_that_keeps_failing_is_given_up_on_with_its_error_and_kept_for_good()
    {
        var (worker, notices, poison) = await WorkerAsync();
        var deadLetters = await ChannelAsync(DeadLetter.QueueOf(QueuedNotice.Queue));
        await PhoneAsync();
        _sender.Failures = int.MaxValue;
        await QueueAsync(notices);

        for (var delivery = 0; delivery < NoticeWorker.MostDeliveries; delivery++)
        {
            await worker.RunOnceAsync(Cancel);
        }

        await new DeadLetterWorker(poison, deadLetters, TimeProvider.System, "test").RunOnceAsync(Cancel);

        (await notices.CountAsync(Cancel)).ShouldBe(0);
        var letter = DeadLetter.Parse((await deadLetters.PeekAsync(1, Cancel)).ShouldHaveSingleItem().Body)!;
        letter.Queue.ShouldBe(notices.Name);
        letter.Delivered.ShouldBe(NoticeWorker.MostDeliveries);
        letter.Error.ShouldContain("503");
        QueuedNotice.Parse(letter.Body)!.Push!.Endpoint.ShouldBe(Phone);
        (await _devices.ListAsync(_mark, Cancel)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_notice_for_a_device_the_account_let_go_of_since_reaches_nothing_and_is_done_with()
    {
        var (worker, notices, poison) = await WorkerAsync();
        await QueueAsync(notices);

        await worker.RunOnceAsync(Cancel);

        _sender.Sent.ShouldBeEmpty();
        (await notices.CountAsync(Cancel)).ShouldBe(0);
        (await poison.CountAsync(Cancel)).ShouldBe(0);
    }

    [Fact]
    public async Task An_item_that_is_no_notice_is_given_up_on_at_once()
    {
        var (worker, notices, poison) = await WorkerAsync();
        await notices.SendAsync("""{"run":"table-1"}""", cancellationToken: Cancel);

        await worker.RunOnceAsync(Cancel);

        (await notices.CountAsync(Cancel)).ShouldBe(0);
        PoisonedWork.Parse((await poison.PeekAsync(1, Cancel)).ShouldHaveSingleItem().Body)!.Error.ShouldContain("Not a notice");
    }

    private async Task<(NoticeWorker Worker, IWorkChannel Notices, IWorkChannel Poison)> WorkerAsync()
    {
        var notices = await ChannelAsync(QueuedNotice.Queue);
        var poison = await ChannelAsync(PoisonedWork.QueueOf(QueuedNotice.Queue));
        return (new NoticeWorker(notices, poison, TimeProvider.System) { Devices = new AccountNotices(_sender, _devices), FirstRetry = TimeSpan.Zero }, notices, poison);
    }

    private Task<IReadOnlyList<PushDevice>> PhoneAsync() =>
        _devices.AddAsync(_mark, new PushDevice(Phone, InMemoryDeviceStore.Key, InMemoryDeviceStore.Auth, default), Cancel);

    private Task<QueuedWork> QueueAsync(IWorkChannel notices) =>
        notices.SendAsync(QueuedNotice.Of(new DevicePush(_mark.Value, Phone, """{"title":"A friend"}""", "friend")).ToJson(), cancellationToken: Cancel);

    /// <summary>A push service that refuses as it is told to: so many times out, or the subscription gone.</summary>
    private sealed class FailingSender : IPushSender
    {
        public int Failures { get; set; }

        public bool Gone { get; set; }

        public List<string> Sent { get; } = [];

        public Task<bool> SendAsync(PushSubscription to, string payload, string topic, CancellationToken cancellationToken = default)
        {
            if (Failures > 0)
            {
                Failures--;
                throw new HttpRequestException("The push service answered 503.");
            }

            if (Gone)
            {
                return Task.FromResult(false);
            }

            Sent.Add(to.Endpoint);
            return Task.FromResult(true);
        }
    }
}
