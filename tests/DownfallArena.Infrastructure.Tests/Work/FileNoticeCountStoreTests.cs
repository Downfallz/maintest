using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Infrastructure.Tests.Work;

public sealed class FileNoticeCountStoreTests : NoticeCountStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"notice-counts-{Guid.CreateVersion7():N}");

    protected override Task<INoticeCountStore> StoreAsync() => Task.FromResult<INoticeCountStore>(new FileNoticeCountStore(_root));

    protected override Task<INoticeCountStore> OtherStoreAsync(INoticeCountStore store) => Task.FromResult<INoticeCountStore>(new FileNoticeCountStore(_root));

    [Fact]
    public async Task A_day_that_does_not_parse_or_holds_a_count_without_its_kind_reads_what_it_can_and_the_next_write_replaces_it()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "2026-11-22.json"), "{ not json", Cancel);
        await File.WriteAllTextAsync(Path.Combine(_root, "2026-11-23.json"), """{ "kinds": [ null, { "sent": 4 }, { "kind": "turn", "sent": 2 } ] }""", Cancel);
        var store = new FileNoticeCountStore(_root);

        var broken = await store.ReadAsync(new DateOnly(2026, 11, 22), Cancel);
        (await store.ReadAsync(new DateOnly(2026, 11, 23), Cancel)).Counts.ShouldBe([new NoticeCount("turn", 2, 0, 0)]);

        broken.Counts.ShouldBeEmpty();
        (await store.WriteAsync(new DateOnly(2026, 11, 22), [new("turn", 1, 0, 0)], broken.Version, Cancel)).ShouldBeTrue();
    }

    /// <summary>A day another revision wrote, with a count this one does not know: adding to it keeps that count (#1151).</summary>
    [Fact]
    public async Task Adding_to_a_day_keeps_the_fields_this_revision_does_not_know()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "2026-11-23.json");
        await File.WriteAllTextAsync(path, NoticeCountFields.Written, Cancel);
        var store = new FileNoticeCountStore(_root);

        var read = await store.ReadAsync(NoticeCountFields.Day, Cancel);
        (await store.WriteAsync(NoticeCountFields.Day, NoticeCount.Sum([.. read.Counts, new("turn", 1, 0, 0)]), read.Version, Cancel)).ShouldBeTrue();

        NoticeCountFields.ShouldKeepThem(await File.ReadAllTextAsync(path, Cancel));
        (await store.ReadAsync(NoticeCountFields.Day, Cancel)).Counts.ShouldBe([new NoticeCount("turn", 3, 0, 1)]);
    }

    [Fact]
    public async Task Forgetting_leaves_a_file_that_is_no_day_where_it_is()
    {
        Directory.CreateDirectory(_root);
        var other = Path.Combine(_root, "notes.json");
        await File.WriteAllTextAsync(other, "{}", Cancel);

        await new FileNoticeCountStore(_root).ForgetBeforeAsync(new DateOnly(2026, 11, 23), Cancel);

        File.Exists(other).ShouldBeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
