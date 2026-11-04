using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FileDeviceStoreTests : DeviceStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"devices-{Guid.CreateVersion7():N}");

    protected override Task<IDeviceStore> StoreAsync() => Task.FromResult<IDeviceStore>(new FileDeviceStore(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
