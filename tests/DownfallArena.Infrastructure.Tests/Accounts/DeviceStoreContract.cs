using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>What every device store promises, played against each adapter.</summary>
public abstract class DeviceStoreContract
{
    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<IDeviceStore> StoreAsync();

    private static PushDevice Phone(string name, int day) =>
        new($"https://push.example/{name}", "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A", "AAECAwQFBgcICQoLDA0ODw", new DateTimeOffset(2026, 11, day, 20, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task A_list_reads_back_as_it_was_saved_and_a_save_replaces_the_one_before()
    {
        var store = await StoreAsync();
        var account = AccountId.New();

        await store.SaveAsync(account, [Phone("one", 1), Phone("two", 2)], Cancel);
        (await store.ListAsync(account, Cancel)).ShouldBe([Phone("one", 1), Phone("two", 2)]);

        await store.SaveAsync(account, [Phone("two", 2)], Cancel);
        (await store.ListAsync(account, Cancel)).ShouldBe([Phone("two", 2)]);
    }

    [Fact]
    public async Task An_account_with_no_devices_lists_none_and_an_empty_save_leaves_none()
    {
        var store = await StoreAsync();
        var account = AccountId.New();

        (await store.ListAsync(account, Cancel)).ShouldBeEmpty();

        await store.SaveAsync(account, [Phone("one", 1)], Cancel);
        await store.SaveAsync(account, [], Cancel);
        (await store.ListAsync(account, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Forgetting_an_account_removes_its_devices_and_forgetting_twice_is_fine()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.SaveAsync(account, [Phone("one", 1)], Cancel);

        await store.ForgetAsync(account, Cancel);
        await store.ForgetAsync(account, Cancel);

        (await store.ListAsync(account, Cancel)).ShouldBeEmpty();
    }
}
