using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FileActivityStoreTests : ActivityStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"activity-{Guid.CreateVersion7():N}");

    protected override Task<IAccountActivityStore> StoreAsync() => Task.FromResult<IAccountActivityStore>(new FileActivityStore(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
