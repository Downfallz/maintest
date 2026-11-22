using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FileNoticeStoreTests : NoticeStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"notices-{Guid.CreateVersion7():N}");

    protected override Task<INoticeStore> StoreAsync() => Task.FromResult<INoticeStore>(new FileNoticeStore(_root));

    [Fact]
    public async Task An_entry_of_a_kind_this_host_does_not_know_is_skipped_and_the_rest_read()
    {
        var account = SharedKernel.Identifiers.AccountId.New();
        var path = Path.Combine(_root, "notices", $"{account.Value}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
            { "entries": [
              { "id": 2, "kind": "tournament", "title": "t", "body": "b", "url": "/", "tag": "x", "at": "2026-11-22T20:00:00+00:00", "read": false },
              null,
              { "id": 1, "kind": "left", "title": "bastien left the table", "body": "b", "url": "/", "tag": "match:t1", "at": "2026-11-22T20:00:00+00:00", "read": false }
            ] }
            """, Cancel);

        var read = await new FileNoticeStore(_root).ReadAsync(account, Cancel);

        read.Entries.ShouldHaveSingleItem().Title.ShouldBe("bastien left the table");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
