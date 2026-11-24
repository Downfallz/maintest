using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class FileNoticeStoreTests : NoticeStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"notices-{Guid.CreateVersion7():N}");

    protected override Task<INoticeStore> StoreAsync() => Task.FromResult<INoticeStore>(new FileNoticeStore(_root));

    protected override Task<INoticeStore> OtherStoreAsync(INoticeStore store) => Task.FromResult<INoticeStore>(new FileNoticeStore(_root));

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

    [Fact]
    public async Task A_log_that_does_not_parse_reads_as_none_and_the_next_write_replaces_it()
    {
        var account = SharedKernel.Identifiers.AccountId.New();
        var path = Path.Combine(_root, "notices", $"{account.Value}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{ not json", Cancel);
        var store = new FileNoticeStore(_root);
        var read = await store.ReadAsync(account, Cancel);
        NoticeEntry[] entries = [new(1, NoticeKind.Left, "bastien left the table", "b", "/", "match:t1", DateTimeOffset.UnixEpoch, false)];

        var written = await store.WriteAsync(account, entries, read.Version, Cancel);

        read.Entries.ShouldBeEmpty();
        written.ShouldBeTrue();
        (await store.ReadAsync(account, Cancel)).Entries.ShouldBe(entries);
    }

    /// <summary>A log another revision wrote, with fields this one does not know: marking it read keeps them (#1211).</summary>
    [Fact]
    public async Task Changing_a_log_keeps_the_fields_this_revision_does_not_know()
    {
        var account = SharedKernel.Identifiers.AccountId.New();
        var path = Path.Combine(_root, "notices", $"{account.Value}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, NoticeFields.Written, Cancel);
        var store = new FileNoticeStore(_root);

        var read = await store.ReadAsync(account, Cancel);
        (await store.WriteAsync(account, NoticeFields.Read(read.Entries), read.Version, Cancel)).ShouldBeTrue();

        NoticeFields.ShouldKeepThem(await File.ReadAllTextAsync(path, Cancel));
    }

    /// <summary>An entry of a kind this revision skips goes at its next write, and its id handed out again does not take its fields.</summary>
    [Fact]
    public async Task An_entry_given_the_id_of_one_of_a_kind_this_revision_skips_does_not_take_its_fields()
    {
        var account = SharedKernel.Identifiers.AccountId.New();
        var path = Path.Combine(_root, "notices", $"{account.Value}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
            { "entries": [ { "id": 2, "kind": "tournament", "title": "t", "body": "b", "url": "/", "tag": "x", "at": "2026-11-22T20:00:00+00:00", "read": false, "icon": "cup" } ] }
            """, Cancel);
        var store = new FileNoticeStore(_root);
        var read = await store.ReadAsync(account, Cancel);
        NoticeEntry[] entries = [new(2, NoticeKind.Left, "bastien left the table", "b", "/", "match:t1", DateTimeOffset.UnixEpoch, false)];

        (await store.WriteAsync(account, entries, read.Version, Cancel)).ShouldBeTrue();

        (await File.ReadAllTextAsync(path, Cancel)).ShouldNotContain("cup");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
