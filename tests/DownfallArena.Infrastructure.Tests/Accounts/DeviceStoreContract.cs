using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>What every device store promises, played against each adapter.</summary>
public abstract class DeviceStoreContract
{
    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<IDeviceStore> StoreAsync();

    /// <summary>
    /// A second store over the same documents, as another process opens it: one that kept nothing of what
    /// <paramref name="store" /> read (#1232). The same store for an adapter with one copy in memory.
    /// </summary>
    protected virtual Task<IDeviceStore> OtherStoreAsync(IDeviceStore store) => Task.FromResult(store);

    private static PushDevice Phone(string name, int day) =>
        new($"https://push.example/{name}", "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A", "AAECAwQFBgcICQoLDA0ODw", new DateTimeOffset(2026, 11, day, 20, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task A_list_reads_back_as_it_was_saved_and_a_save_replaces_the_one_before()
    {
        var store = await StoreAsync();
        var account = AccountId.New();

        (await store.SaveAsync(account, [Phone("one", 1), Phone("two", 2)], null, Cancel)).ShouldBeTrue();
        (await store.ListAsync(account, Cancel)).ShouldBe([Phone("one", 1), Phone("two", 2)]);

        (await store.SaveAsync(account, [Phone("two", 2)], (await store.ReadAsync(account, Cancel)).Version, Cancel)).ShouldBeTrue();
        (await store.ListAsync(account, Cancel)).ShouldBe([Phone("two", 2)]);
    }

    [Fact]
    public async Task A_gone_endpoint_reads_back_gone_beside_the_devices()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        var gone = new PushDevice("https://push.example/dead", string.Empty, string.Empty, new DateTimeOffset(2026, 11, 3, 20, 0, 0, TimeSpan.Zero), Gone: true);

        (await store.SaveAsync(account, [gone, Phone("one", 1)], null, Cancel)).ShouldBeTrue();

        (await store.ListAsync(account, Cancel)).ShouldBe([gone, Phone("one", 1)]);
    }

    [Fact]
    public async Task An_entry_without_keys_reads_gone_though_an_older_image_dropped_its_flag()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        var keyless = new PushDevice("https://push.example/dead", string.Empty, string.Empty, new DateTimeOffset(2026, 11, 3, 20, 0, 0, TimeSpan.Zero));

        await store.SaveAsync(account, [keyless], null, Cancel);

        (await store.ListAsync(account, Cancel)).ShouldHaveSingleItem().Gone.ShouldBeTrue();
    }

    [Fact]
    public async Task An_account_with_no_devices_lists_none_and_an_empty_save_leaves_none()
    {
        var store = await StoreAsync();
        var account = AccountId.New();

        (await store.ListAsync(account, Cancel)).ShouldBeEmpty();

        await store.SaveAsync(account, [Phone("one", 1)], null, Cancel);
        (await store.SaveAsync(account, [], (await store.ReadAsync(account, Cancel)).Version, Cancel)).ShouldBeTrue();
        (await store.ListAsync(account, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Forgetting_an_account_removes_its_devices_and_forgetting_twice_is_fine()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.SaveAsync(account, [Phone("one", 1)], null, Cancel);

        await store.ForgetAsync(account, Cancel);
        await store.ForgetAsync(account, Cancel);

        (await store.ListAsync(account, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_save_over_a_version_another_write_replaced_is_refused_and_leaves_that_write()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.SaveAsync(account, [Phone("one", 1)], null, Cancel);
        var stale = (await store.ReadAsync(account, Cancel)).Version;
        await store.SaveAsync(account, [Phone("one", 1), Phone("two", 2)], stale, Cancel);

        var saved = await store.SaveAsync(account, [Phone("three", 3)], stale, Cancel);

        saved.ShouldBeFalse();
        (await store.ListAsync(account, Cancel)).ShouldBe([Phone("one", 1), Phone("two", 2)]);
    }

    [Fact]
    public async Task A_first_save_is_refused_once_another_one_made_the_list()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.SaveAsync(account, [Phone("one", 1)], null, Cancel);

        var saved = await store.SaveAsync(account, [Phone("two", 2)], null, Cancel);

        saved.ShouldBeFalse();
        (await store.ListAsync(account, Cancel)).ShouldBe([Phone("one", 1)]);
    }

    [Fact]
    public async Task Emptying_a_list_over_a_version_another_write_replaced_is_refused_and_leaves_that_write()
    {
        var store = await StoreAsync();
        var account = AccountId.New();
        await store.SaveAsync(account, [Phone("one", 1)], null, Cancel);
        var stale = (await store.ReadAsync(account, Cancel)).Version;
        await store.SaveAsync(account, [Phone("one", 1), Phone("two", 2)], stale, Cancel);

        var saved = await store.SaveAsync(account, [], stale, Cancel);

        saved.ShouldBeFalse();
        (await store.ListAsync(account, Cancel)).ShouldBe([Phone("one", 1), Phone("two", 2)]);
    }

    [Fact]
    public async Task Forgetting_an_account_on_a_store_that_never_wrote_anything_is_fine()
    {
        var store = await StoreAsync();

        await store.ForgetAsync(AccountId.New(), Cancel);

        (await store.ListAsync(AccountId.New(), Cancel)).ShouldBeEmpty();
    }

    /// <summary>The text this store kept of its read does not let its save past another store's since (#1232).</summary>
    [Fact]
    public async Task A_save_over_the_version_read_is_refused_once_another_store_saved_since()
    {
        var store = await StoreAsync();
        var other = await OtherStoreAsync(store);
        var account = AccountId.New();
        await store.SaveAsync(account, [Phone("one", 1)], null, Cancel);
        var read = await store.ReadAsync(account, Cancel);
        (await other.SaveAsync(account, [Phone("one", 1), Phone("two", 2)], (await other.ReadAsync(account, Cancel)).Version, Cancel)).ShouldBeTrue();

        var saved = await store.SaveAsync(account, [Phone("one", 1), Phone("three", 3)], read.Version, Cancel);

        saved.ShouldBeFalse();
        (await store.ListAsync(account, Cancel)).ShouldBe([Phone("one", 1), Phone("two", 2)]);
    }

    /// <summary>Nor past another store's forgetting the account: its devices stay gone.</summary>
    [Fact]
    public async Task A_save_over_the_version_read_is_refused_once_another_store_forgot_the_account()
    {
        var store = await StoreAsync();
        var other = await OtherStoreAsync(store);
        var account = AccountId.New();
        await store.SaveAsync(account, [Phone("one", 1)], null, Cancel);
        var read = await store.ReadAsync(account, Cancel);
        await other.ForgetAsync(account, Cancel);

        var saved = await store.SaveAsync(account, [Phone("one", 1), Phone("two", 2)], read.Version, Cancel);

        saved.ShouldBeFalse();
        (await store.ListAsync(account, Cancel)).ShouldBeEmpty();
    }
}
