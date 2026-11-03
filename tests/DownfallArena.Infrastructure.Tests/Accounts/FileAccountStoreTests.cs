using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FileAccountStoreTests : AccountStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"accounts-{Guid.CreateVersion7():N}");

    protected override Task<IAccountStore> StoreAsync() => Task.FromResult<IAccountStore>(new FileAccountStore(_root));

    [Fact]
    public async Task Nothing_but_the_records_is_left_in_the_directory()
    {
        var store = await StoreAsync();
        await store.TryBindSubjectAsync(new("google", "1"), SharedKernel.Identifiers.AccountId.New(), Cancel);
        await store.TryBindSubjectAsync(new("google", "1"), SharedKernel.Identifiers.AccountId.New(), Cancel);

        Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
