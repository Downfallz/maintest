using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FileDeviceStoreTests : DeviceStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"devices-{Guid.CreateVersion7():N}");

    protected override Task<IDeviceStore> StoreAsync() => Task.FromResult<IDeviceStore>(new FileDeviceStore(_root));

    protected override Task<IDeviceStore> OtherStoreAsync(IDeviceStore store) => Task.FromResult<IDeviceStore>(new FileDeviceStore(_root));

    /// <summary>A list another revision wrote, with fields this one does not know: adding a device keeps them (#1211).</summary>
    [Fact]
    public async Task Changing_a_list_keeps_the_fields_this_revision_does_not_know()
    {
        var account = SharedKernel.Identifiers.AccountId.New();
        var path = Path.Combine(_root, "devices", $"{account.Value}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, DeviceFields.Written, Cancel);
        var store = new FileDeviceStore(_root);

        var read = await store.ReadAsync(account, Cancel);
        (await store.SaveAsync(account, DeviceFields.Added(read.Devices), read.Version, Cancel)).ShouldBeTrue();

        DeviceFields.ShouldKeepThem(await File.ReadAllTextAsync(path, Cancel));
    }

    /// <summary>A device marked gone is kept without its keys (ADR 0203), and without what a later revision said of it.</summary>
    [Fact]
    public async Task A_device_marked_gone_drops_the_fields_this_revision_does_not_know()
    {
        var account = SharedKernel.Identifiers.AccountId.New();
        var path = Path.Combine(_root, "devices", $"{account.Value}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, DeviceFields.Written, Cancel);
        var store = new FileDeviceStore(_root);
        var read = await store.ReadAsync(account, Cancel);

        (await store.SaveAsync(account, [read.Devices[0] with { P256dh = string.Empty, Auth = string.Empty, Gone = true }], read.Version, Cancel)).ShouldBeTrue();

        (await File.ReadAllTextAsync(path, Cancel)).ShouldNotContain("Pixel");
    }

    /// <summary>A browser subscribed again on the same endpoint is another subscription: it does not take what was said of the one before.</summary>
    [Fact]
    public async Task A_device_subscribed_again_with_another_key_drops_the_fields_this_revision_does_not_know()
    {
        var account = SharedKernel.Identifiers.AccountId.New();
        var path = Path.Combine(_root, "devices", $"{account.Value}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, DeviceFields.Written, Cancel);
        var store = new FileDeviceStore(_root);
        var read = await store.ReadAsync(account, Cancel);

        (await store.SaveAsync(account, [read.Devices[0] with { P256dh = "BP8" }], read.Version, Cancel)).ShouldBeTrue();

        (await File.ReadAllTextAsync(path, Cancel)).ShouldNotContain("Pixel");
    }

    /// <summary>A file read is kept for the write after it, and its push keys let go unused within two lifetimes (#1260, ADR 0203).</summary>
    [Fact]
    public async Task A_list_read_is_kept_for_the_write_after_it_and_let_go_within_two_lifetimes_if_none_comes()
    {
        var account = SharedKernel.Identifiers.AccountId.New();
        var path = Path.Combine(_root, "devices", $"{account.Value}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, DeviceFields.Written, Cancel);
        var time = new FakeTimeProvider();
        var store = new FileDeviceStore(_root, time);

        await store.ListAsync(account, Cancel);
        store.Kept.ShouldBe(0);
        await store.ReadAsync(account, Cancel);
        store.Kept.ShouldBe(1);
        time.Advance(TimeSpan.FromSeconds(2 * TextsRead<SharedKernel.Identifiers.AccountId>.LifetimeSeconds));

        store.Kept.ShouldBe(0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
