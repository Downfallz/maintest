using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Accounts;

/// <summary>
/// An account's devices (friends plan, stage 1): a browser is one device however often it registers, the sixth
/// replaces the oldest, and a device goes when its browser or its push service says so.
/// </summary>
public sealed class AccountDevicesTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly AccountId _mark = AccountId.New();
    private readonly MovableClock _clock = new(new DateTimeOffset(2026, 11, 4, 20, 0, 0, TimeSpan.Zero));
    private readonly InMemoryDevices _store = new();
    private readonly AccountDevices _devices;

    public AccountDevicesTests()
    {
        _devices = new AccountDevices(_store, _clock);
    }

    private static PushDevice Phone(string name) => new($"https://push.example/{name}", $"key-{name}", $"auth-{name}", default);

    [Fact]
    public async Task A_device_is_stamped_when_it_registers_and_listed_afterwards()
    {
        var held = await _devices.AddAsync(_mark, Phone("one"), Cancel);

        var device = held.ShouldHaveSingleItem();
        device.Endpoint.ShouldBe("https://push.example/one");
        device.AddedAt.ShouldBe(_clock.GetUtcNow());
        (await _devices.ListAsync(_mark, Cancel)).ShouldBe(held);
    }

    [Fact]
    public async Task The_same_endpoint_registered_twice_is_one_device_with_the_keys_it_sent_last()
    {
        await _devices.AddAsync(_mark, Phone("one"), Cancel);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var held = await _devices.AddAsync(_mark, Phone("one") with { Auth = "rotated" }, Cancel);

        var device = held.ShouldHaveSingleItem();
        device.Auth.ShouldBe("rotated");
        device.AddedAt.ShouldBe(_clock.GetUtcNow(), "the registration that counts is the latest");
    }

    [Fact]
    public async Task A_sixth_device_replaces_the_oldest()
    {
        for (var index = 0; index < AccountDevices.MostPerAccount; index++)
        {
            await _devices.AddAsync(_mark, Phone($"device-{index}"), Cancel);
            _clock.Advance(TimeSpan.FromDays(1));
        }

        var held = await _devices.AddAsync(_mark, Phone("newest"), Cancel);

        held.Count.ShouldBe(AccountDevices.MostPerAccount);
        held.Select(device => device.Endpoint).ShouldNotContain("https://push.example/device-0");
        held[^1].Endpoint.ShouldBe("https://push.example/newest");
    }

    [Fact]
    public async Task A_device_is_removed_by_its_endpoint_and_the_others_stay()
    {
        await _devices.AddAsync(_mark, Phone("one"), Cancel);
        await _devices.AddAsync(_mark, Phone("two"), Cancel);

        (await _devices.RemoveAsync(_mark, "https://push.example/one", Cancel)).ShouldBeTrue();

        (await _devices.ListAsync(_mark, Cancel)).ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example/two");
        (await _devices.RemoveAsync(_mark, "https://push.example/one", Cancel)).ShouldBeFalse("already gone");
    }

    [Fact]
    public async Task Forgetting_an_account_leaves_it_no_device_and_touches_nobody_else_s()
    {
        var other = AccountId.New();
        await _devices.AddAsync(_mark, Phone("one"), Cancel);
        await _devices.AddAsync(other, Phone("theirs"), Cancel);

        await _devices.ForgetAsync(_mark, Cancel);

        (await _devices.ListAsync(_mark, Cancel)).ShouldBeEmpty();
        (await _devices.ListAsync(other, Cancel)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_device_forgotten_by_another_process_while_one_registers_stays_forgotten_and_the_new_one_is_kept()
    {
        await _devices.AddAsync(_mark, Phone("dead"), Cancel);
        // The worker forgets the dead device between this process's read and its write (ADR 0201).
        _store.BeforeNextSave = () => new AccountDevices(_store, _clock).RemoveAsync(_mark, "https://push.example/dead", Cancel);

        await _devices.AddAsync(_mark, Phone("new"), Cancel);

        (await _devices.ListAsync(_mark, Cancel)).Select(device => device.Endpoint).ShouldBe(["https://push.example/new"]);
    }

    [Fact]
    public async Task A_device_its_push_service_says_is_gone_is_listed_no_more_and_its_endpoint_is_remembered_as_gone()
    {
        await _devices.AddAsync(_mark, Phone("one"), Cancel);
        await _devices.AddAsync(_mark, Phone("two"), Cancel);

        (await _devices.MarkGoneAsync(_mark, "https://push.example/one", Cancel)).ShouldBeTrue();

        (await _devices.ListAsync(_mark, Cancel)).Select(device => device.Endpoint).ShouldBe(["https://push.example/two"]);
        (await _devices.IsGoneAsync(_mark, "https://push.example/one", Cancel)).ShouldBeTrue();
        (await _devices.IsGoneAsync(_mark, "https://push.example/two", Cancel)).ShouldBeFalse();
        (await _store.ListAsync(_mark, Cancel)).Single(device => device.Gone).P256dh.ShouldBeEmpty("a gone endpoint keeps no key");
    }

    [Fact]
    public async Task Gone_endpoints_take_no_place_of_a_device_and_only_the_latest_are_remembered()
    {
        for (var index = 0; index < AccountDevices.MostGone + 2; index++)
        {
            await _devices.AddAsync(_mark, Phone($"dead-{index}"), Cancel);
            await _devices.MarkGoneAsync(_mark, $"https://push.example/dead-{index}", Cancel);
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        for (var index = 0; index < AccountDevices.MostPerAccount; index++)
        {
            await _devices.AddAsync(_mark, Phone($"live-{index}"), Cancel);
        }

        (await _devices.ListAsync(_mark, Cancel)).Count.ShouldBe(AccountDevices.MostPerAccount);
        (await _store.ListAsync(_mark, Cancel)).Count(device => device.Gone).ShouldBe(AccountDevices.MostGone);
        (await _devices.IsGoneAsync(_mark, "https://push.example/dead-0", Cancel)).ShouldBeFalse("the oldest gone endpoint is let go of");
        (await _devices.IsGoneAsync(_mark, $"https://push.example/dead-{AccountDevices.MostGone + 1}", Cancel)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_gone_endpoint_the_account_no_longer_holds_writes_nothing()
    {
        (await _devices.MarkGoneAsync(_mark, "https://push.example/forgotten", Cancel)).ShouldBeFalse();

        (await _store.ReadAsync(_mark, Cancel)).Version.ShouldBeNull("a list forgotten meanwhile does not come back");
    }

    [Fact]
    public async Task Forgetting_a_gone_endpoint_keeps_it_remembered_as_gone()
    {
        await _devices.AddAsync(_mark, Phone("one"), Cancel);
        await _devices.MarkGoneAsync(_mark, "https://push.example/one", Cancel);

        (await _devices.RemoveAsync(_mark, "https://push.example/one", Cancel)).ShouldBeFalse();

        (await _devices.IsGoneAsync(_mark, "https://push.example/one", Cancel)).ShouldBeTrue();
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class InMemoryDevices : IDeviceStore
    {
        private readonly Dictionary<AccountId, (IReadOnlyList<PushDevice> Devices, string Version)> _held = [];
        private long _written;

        public Func<Task>? BeforeNextSave { get; set; }

        public Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default) =>
            Task.FromResult(_held.TryGetValue(account, out var held) ? held.Devices : []);

        public Task<DeviceListRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default) =>
            Task.FromResult(_held.TryGetValue(account, out var held) ? new DeviceListRead(held.Devices, held.Version) : new DeviceListRead([], null));

        public async Task<bool> SaveAsync(AccountId account, IReadOnlyList<PushDevice> devices, string? version, CancellationToken cancellationToken = default)
        {
            if (BeforeNextSave is { } before)
            {
                BeforeNextSave = null;
                await before();
            }

            if ((_held.TryGetValue(account, out var held) ? held.Version : null) != version)
            {
                return false;
            }

            _held[account] = ([.. devices], $"{++_written}");
            return true;
        }

        public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
        {
            _held.Remove(account);
            return Task.CompletedTask;
        }
    }
}
