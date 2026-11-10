using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Tests.Resources;

namespace DownfallArena.Infrastructure.Tests.Learning;

/// <summary>The store over a root directory: a run is one directory under it, listed and removed as such.</summary>
public sealed class FileArtifactStoreTests
{
    /// <summary>
    /// A document written on a condition is written only over the content its writer read: two writers that
    /// read the same content cannot both write after it, and the second reads again (ADR 0159).
    /// </summary>
    [Fact]
    public async Task A_document_is_written_on_a_condition_only_over_the_content_its_writer_read()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path);
        var writer = store.Writer("run-1");
        var reader = store.Reader("run-1");

        var first = await writer.WriteJsonIfAsync("table.json", new { Revision = 1 }, expected: null, TestContext.Current.CancellationToken);
        var again = await writer.WriteJsonIfAsync("table.json", new { Revision = 1 }, expected: null, TestContext.Current.CancellationToken);
        var read = (await reader.ReadTaggedAsync("table.json", TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var second = await writer.WriteJsonIfAsync("table.json", new { Revision = 2 }, read.Tag, TestContext.Current.CancellationToken);
        var late = await writer.WriteJsonIfAsync("table.json", new { Revision = 3 }, read.Tag, TestContext.Current.CancellationToken);

        first.ShouldBe(read.Tag);
        again.ShouldBeNull("there was a document already");
        second.ShouldNotBeNull();
        late.ShouldBeNull("the document moved since it was read");
        (await reader.ReadTaggedAsync("table.json", TestContext.Current.CancellationToken)).ShouldNotBeNull().Tag.ShouldBe(second);
        (await reader.ReadTextAsync("table.json", TestContext.Current.CancellationToken)).ShouldNotBeNull().ShouldContain("\"revision\": 2");
    }

    [Fact]
    public async Task A_document_nobody_wrote_reads_as_none()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path);

        var read = await store.Reader("run-1").ReadTaggedAsync("table.json", TestContext.Current.CancellationToken);

        read.ShouldBeNull();
    }

    [Fact]
    public async Task The_runs_are_the_directories_under_the_root_in_ordinal_order()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path);
        await store.Writer("20260101-100000-bb").WriteJsonAsync("manifest.json", new { Matches = 0 }, TestContext.Current.CancellationToken);
        await store.Writer("20260101-090000-aa").WriteJsonAsync("manifest.json", new { Matches = 1 }, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "stray.txt"), "not a run", TestContext.Current.CancellationToken);

        var runs = await store.RunsAsync(TestContext.Current.CancellationToken);

        runs.ShouldBe(["20260101-090000-aa", "20260101-100000-bb"]);
    }

    [Fact]
    public async Task A_root_nobody_wrote_to_yet_holds_no_run()
    {
        var store = new FileArtifactStore(Path.Combine(Path.GetTempPath(), $"downfall-store-{Guid.NewGuid():N}"));

        (await store.RunsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        (await store.DeleteAsync("nowhere", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Deleting_a_run_removes_everything_under_it_and_nothing_beside_it()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path);
        await store.Writer("run-1").WriteJsonAsync("traces/a.json", new { Sequence = 1 }, TestContext.Current.CancellationToken);
        await store.Writer("run-1").AppendJsonLinesAsync("notes.jsonl", [new { Kind = "note" }], TestContext.Current.CancellationToken);
        await store.Writer("run-2").WriteJsonAsync("manifest.json", new { Matches = 0 }, TestContext.Current.CancellationToken);

        (await store.DeleteAsync("run-1", TestContext.Current.CancellationToken)).ShouldBeTrue();

        Directory.Exists(Path.Combine(directory.Path, "run-1")).ShouldBeFalse();
        (await store.RunsAsync(TestContext.Current.CancellationToken)).ShouldBe(["run-2"]);
        (await store.DeleteAsync("run-1", TestContext.Current.CancellationToken)).ShouldBeFalse("it is already gone");
    }

    [Fact]
    public async Task A_run_name_that_leaves_the_root_is_refused_before_anything_is_deleted()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path);

        await Should.ThrowAsync<ArgumentException>(() => store.DeleteAsync("..", TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => store.DeleteAsync("a/b", TestContext.Current.CancellationToken));
    }
}
