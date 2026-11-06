using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FilePreferenceStoreTests : PreferenceStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"preferences-{Guid.CreateVersion7():N}");

    protected override Task<IPreferenceStore> StoreAsync() => Task.FromResult<IPreferenceStore>(new FilePreferenceStore(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
