using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FileFriendStoreTests : FriendStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"friends-{Guid.CreateVersion7():N}");

    protected override Task<IFriendStore> StoreAsync() => Task.FromResult<IFriendStore>(new FileFriendStore(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
