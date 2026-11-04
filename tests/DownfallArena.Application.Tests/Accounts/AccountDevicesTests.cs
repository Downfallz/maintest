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

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
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
