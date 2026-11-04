using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Table;
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
        _notices.IsDead("https://push.example/old").ShouldBeTrue();
        _notices.IsDead("https://push.example/new").ShouldBeFalse();
    }

    [Fact]
    public async Task A_service_that_is_out_loses_the_notice_and_keeps_the_device()
    {
        await _devices.AddAsync(_mark, Phone("phone"), Cancel);
        _sender.Failure = new HttpRequestException("The push service answered 503.");

        var reached = await _notices.SendAsync(_mark, "{}", "friend", Cancel);

        reached.ShouldBe(0);
        (await _devices.ListAsync(_mark, Cancel)).ShouldHaveSingleItem();
        _notices.IsDead("https://push.example/phone").ShouldBeFalse();
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

    private sealed class Sender : IPushSender
    {
        public List<(PushSubscription To, string Payload, string Topic)> Sent { get; } = [];

        public HashSet<string> Gone { get; } = new(StringComparer.Ordinal);

        public Exception? Failure { get; set; }

        public Task<bool> SendAsync(PushSubscription to, string payload, string topic, CancellationToken cancellationToken = default)
        {
            if (Failure is { } failure)
            {
                return Task.FromException<bool>(failure);
            }

            if (Gone.Contains(to.Endpoint))
            {
                return Task.FromResult(false);
            }

            Sent.Add((to, payload, topic));
            return Task.FromResult(true);
        }
    }

    private sealed class InMemoryDevices : IDeviceStore
    {
        private readonly Dictionary<AccountId, IReadOnlyList<PushDevice>> _held = [];

        public Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default) =>
            Task.FromResult(_held.TryGetValue(account, out var devices) ? devices : []);

        public Task SaveAsync(AccountId account, IReadOnlyList<PushDevice> devices, CancellationToken cancellationToken = default)
        {
            _held[account] = [.. devices];
            return Task.CompletedTask;
        }

        public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
        {
            _held.Remove(account);
            return Task.CompletedTask;
        }
    }
}
