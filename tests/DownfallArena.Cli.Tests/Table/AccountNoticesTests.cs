using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Work;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A notice to an account reaches every device it registered, once each (friends plan, stage 1); a device its
/// push service let go of is forgotten and its page answered 410; a service that is out loses the notice and
/// nothing else.
/// </summary>
public sealed class AccountNoticesTests
{
    private const string Key = "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A";
    private const string Auth = "AAECAwQFBgcICQoLDA0ODw";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly AccountId _mark = AccountId.New();
    private readonly Sender _sender = new();
    private readonly AccountDevices _devices = new(new InMemoryDevices(), TimeProvider.System);
    private readonly AccountNotices _notices;

    public AccountNoticesTests()
    {
        _notices = new AccountNotices(_sender, _devices);
    }

    private static PushDevice Phone(string name) => new($"https://push.example/{name}", Key, Auth, default);

    [Fact]
    public async Task A_notice_goes_to_each_device_once_under_its_topic()
    {
        await _devices.AddAsync(_mark, Phone("phone"), Cancel);
        await _devices.AddAsync(_mark, Phone("laptop"), Cancel);
        await _devices.AddAsync(_mark, Phone("phone"), Cancel);

        var reached = await _notices.SendAsync(_mark, AccountNotices.Payload("A friend", "bastien wants to be your friend", "/lobby#friends", "friend:acct"), "friend", Cancel);

        reached.ShouldBe(2);
        _sender.Sent.Select(sent => sent.To.Endpoint).Order(StringComparer.Ordinal).ShouldBe(["https://push.example/laptop", "https://push.example/phone"]);
        _sender.Sent.ShouldAllBe(sent => sent.Topic == "friend");
        var notice = JsonDocument.Parse(_sender.Sent[0].Payload).RootElement;
        notice.GetProperty("title").GetString().ShouldBe("A friend");
        notice.GetProperty("url").GetString().ShouldBe("/lobby#friends");
        notice.TryGetProperty("test", out _).ShouldBeFalse("only a test notice says it is one");
    }

    [Fact]
    public async Task A_device_its_push_service_says_is_gone_is_forgotten_and_remembered_as_dead()
    {
        await _devices.AddAsync(_mark, Phone("old"), Cancel);
        await _devices.AddAsync(_mark, Phone("new"), Cancel);
        _sender.Gone.Add("https://push.example/old");

        var reached = await _notices.SendAsync(_mark, "{}", "friend", Cancel);

        reached.ShouldBe(1);
        (await _devices.ListAsync(_mark, Cancel)).ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example/new");
        (await _notices.IsGoneAsync(_mark, "https://push.example/old", Cancel)).ShouldBeTrue();
        (await _notices.IsGoneAsync(_mark, "https://push.example/new", Cancel)).ShouldBeFalse();
        (await new AccountNotices(_sender, _devices).IsGoneAsync(_mark, "https://push.example/old", Cancel)).ShouldBeTrue("another process reads it from the devices");
    }

    /// <summary>A device kept on a push service the host does not send to reaches nobody and is forgotten, as a gone one is (#1209).</summary>
    [Fact]
    public async Task A_device_on_a_push_service_the_host_does_not_send_to_is_not_counted_and_is_forgotten()
    {
        await _devices.AddAsync(_mark, Phone("old"), Cancel);
        await _devices.AddAsync(_mark, Phone("new"), Cancel);
        _sender.Unknown.Add("https://push.example/old");

        var reached = await _notices.SendAsync(_mark, "{}", "friend", Cancel);

        reached.ShouldBe(1);
        (await _devices.ListAsync(_mark, Cancel)).ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example/new");
    }

    /// <summary>The queued send to such a device is done with as gone, not sent (#1209).</summary>
    [Fact]
    public async Task A_queued_notice_to_a_device_on_a_push_service_the_host_does_not_send_to_is_gone_and_forgets_it()
    {
        await _devices.AddAsync(_mark, Phone("old"), Cancel);
        _sender.Unknown.Add("https://push.example/old");

        var sent = await _notices.SendOneAsync(new DevicePush(_mark.Value, "https://push.example/old", "{}", "friend"), Cancel);

        sent.ShouldBe(DeviceSend.Gone);
        (await _devices.ListAsync(_mark, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_service_that_is_out_loses_the_notice_and_keeps_the_device()
    {
        await _devices.AddAsync(_mark, Phone("phone"), Cancel);
        _sender.Failure = new HttpRequestException("The push service answered 503.");

        var reached = await _notices.SendAsync(_mark, "{}", "friend", Cancel);

        reached.ShouldBe(0);
        (await _devices.ListAsync(_mark, Cancel)).ShouldHaveSingleItem();
        (await _notices.IsGoneAsync(_mark, "https://push.example/phone", Cancel)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_queued_notice_is_one_item_a_device_but_the_ones_left_out_and_names_no_key()
    {
        await _devices.AddAsync(_mark, Phone("phone"), Cancel);
        await _devices.AddAsync(_mark, Phone("laptop"), Cancel);
        var outbox = new InMemoryWorkChannel(QueuedNotice.Queue, TimeProvider.System);

        var queued = await _notices.EnqueueAsync(outbox, _mark, "{}", "friend", ["https://push.example/laptop"], Cancel);

        queued.ShouldBe(1);
        _sender.Sent.ShouldBeEmpty("nothing is sent in the request");
        var body = outbox.Bodies.ShouldHaveSingleItem();
        QueuedNotice.Parse(body)!.Push.ShouldBe(new DevicePush(_mark.Value, "https://push.example/phone", "{}", "friend"));
        body.ShouldNotContain(Key);
    }

    [Fact]
    public async Task A_queued_notice_reaches_its_device_and_one_for_a_device_let_go_of_since_reaches_nothing()
    {
        await _devices.AddAsync(_mark, Phone("phone"), Cancel);

        var sent = await _notices.SendOneAsync(new DevicePush(_mark.Value, "https://push.example/phone", "{}", "friend"), Cancel);
        var dropped = await _notices.SendOneAsync(new DevicePush(_mark.Value, "https://push.example/signed-out", "{}", "friend"), Cancel);

        sent.ShouldBe(DeviceSend.Sent);
        dropped.ShouldBe(DeviceSend.NotADevice);
        _sender.Sent.ShouldHaveSingleItem().To.ShouldBe(new PushSubscription("https://push.example/phone", Key, Auth));
    }

    [Fact]
    public async Task A_test_goes_to_the_one_device_asked_for_and_says_what_came_of_it()
    {
        await _devices.AddAsync(_mark, Phone("phone"), Cancel);
        await _devices.AddAsync(_mark, Phone("laptop"), Cancel);

        var sent = await _notices.TestAsync(_mark, "https://push.example/phone", Cancel);
        var unknown = await _notices.TestAsync(_mark, "https://push.example/tablet", Cancel);

        sent.Sent.ShouldBeTrue();
        _sender.Sent.ShouldHaveSingleItem().To.Endpoint.ShouldBe("https://push.example/phone");
        _sender.Sent[0].Topic.ShouldBe("test");
        JsonDocument.Parse(_sender.Sent[0].Payload).RootElement.GetProperty("test").GetBoolean().ShouldBeTrue();
        unknown.Sent.ShouldBeFalse();
        unknown.Message.ShouldContain("not one of your devices");
    }

    [Fact]
    public async Task A_test_to_a_device_that_is_gone_forgets_it()
    {
        await _devices.AddAsync(_mark, Phone("phone"), Cancel);
        _sender.Gone.Add("https://push.example/phone");

        var test = await _notices.TestAsync(_mark, "https://push.example/phone", Cancel);

        test.Sent.ShouldBeFalse();
        test.Message.ShouldContain("gone");
        (await _devices.ListAsync(_mark, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_test_the_push_service_refuses_says_its_status_and_not_its_words()
    {
        await _devices.AddAsync(_mark, Phone("phone"), Cancel);
        _sender.Failure = new HttpRequestException("The push service answered 403: vapid key for acct_secret", null, System.Net.HttpStatusCode.Forbidden);

        var test = await _notices.TestAsync(_mark, "https://push.example/phone", Cancel);

        test.Sent.ShouldBeFalse();
        test.Message.ShouldBe("The notification service refused it (403).");
    }

    [Fact]
    public async Task A_test_whose_keys_cannot_be_used_says_so_rather_than_to_try_again()
    {
        await _devices.AddAsync(_mark, Phone("phone"), Cancel);
        _sender.Failure = new System.Security.Cryptography.CryptographicException("The point is not on the curve.");

        var test = await _notices.TestAsync(_mark, "https://push.example/phone", Cancel);

        test.Message.ShouldBe("This device's subscription could not be used. Turn notifications off and on again.");
    }

    private sealed class Sender : IPushSender
    {
        public List<(PushSubscription To, string Payload, string Topic)> Sent { get; } = [];

        public HashSet<string> Gone { get; } = new(StringComparer.Ordinal);

        /// <summary>The endpoints on no push service the host sends to: never sent to (#1209).</summary>
        public HashSet<string> Unknown { get; } = new(StringComparer.Ordinal);

        public Exception? Failure { get; set; }

        public Task<PushSent> SendAsync(PushSubscription to, string payload, string topic, CancellationToken cancellationToken = default)
        {
            if (Failure is { } failure)
            {
                return Task.FromException<PushSent>(failure);
            }

            if (Gone.Contains(to.Endpoint))
            {
                return Task.FromResult(PushSent.Gone);
            }

            if (Unknown.Contains(to.Endpoint))
            {
                return Task.FromResult(PushSent.UnknownService);
            }

            Sent.Add((to, payload, topic));
            return Task.FromResult(PushSent.Taken);
        }
    }

    private sealed class InMemoryDevices : IDeviceStore
    {
        private readonly Dictionary<AccountId, (IReadOnlyList<PushDevice> Devices, string Version)> _held = [];
        private long _written;

        public Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default) =>
            Task.FromResult(_held.TryGetValue(account, out var held) ? held.Devices : []);

        public Task<DeviceListRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default) =>
            Task.FromResult(_held.TryGetValue(account, out var held) ? new DeviceListRead(held.Devices, held.Version) : new DeviceListRead([], null));

        public Task<bool> SaveAsync(AccountId account, IReadOnlyList<PushDevice> devices, string? version, CancellationToken cancellationToken = default)
        {
            if ((_held.TryGetValue(account, out var held) ? held.Version : null) != version)
            {
                return Task.FromResult(false);
            }

            _held[account] = ([.. devices], $"{++_written}");
            return Task.FromResult(true);
        }

        public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
        {
            _held.Remove(account);
            return Task.CompletedTask;
        }
    }
}
