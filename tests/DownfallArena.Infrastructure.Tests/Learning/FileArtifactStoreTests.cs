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
    public async Task A_document_is_deleted_on_a_condition_only_over_the_content_its_writer_wrote()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path);
        var writer = store.Writer("run-1");
        var tag = (await writer.WriteJsonIfAsync("reactions.json", new { Revision = 1 }, expected: null, TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var stale = await writer.DeleteIfAsync("reactions.json", "not-the-tag", TestContext.Current.CancellationToken);
        var deleted = await writer.DeleteIfAsync("reactions.json", tag, TestContext.Current.CancellationToken);
        var again = await writer.DeleteIfAsync("reactions.json", tag, TestContext.Current.CancellationToken);

        stale.ShouldBeFalse("the document is not the one that was written");
        deleted.ShouldBeTrue();
        again.ShouldBeFalse("there is nothing left to delete");
        (await store.Reader("run-1").ReadTextAsync("reactions.json", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Taking_a_document_back_from_a_deleted_run_does_not_bring_the_run_back()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path);
        var writer = store.Writer("run-1");
        await writer.WriteJsonAsync("table.json", new { Revision = 1 }, TestContext.Current.CancellationToken);
        var tag = (await writer.WriteJsonIfAsync("seen-player1.json", new { Sequence = 1 }, expected: null, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await store.DeleteAsync("run-1", first: null, TestContext.Current.CancellationToken);

        var deleted = await writer.DeleteIfAsync("seen-player1.json", tag, TestContext.Current.CancellationToken);

        deleted.ShouldBeFalse();
        (await store.RunsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Taking_back_the_last_document_of_a_run_leaves_no_run()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path);
        var writer = store.Writer("run-1");
        var tag = (await writer.WriteJsonIfAsync("seen-player1.json", new { Sequence = 1 }, expected: null, TestContext.Current.CancellationToken)).ShouldNotBeNull();

        await writer.DeleteIfAsync("seen-player1.json", tag, TestContext.Current.CancellationToken);

        (await store.RunsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
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
        (await store.DeleteAsync("nowhere", first: null, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Deleting_a_run_removes_everything_under_it_and_nothing_beside_it()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path);
        await store.Writer("run-1").WriteJsonAsync("traces/a.json", new { Sequence = 1 }, TestContext.Current.CancellationToken);
        await store.Writer("run-1").AppendJsonLinesAsync("notes.jsonl", [new { Kind = "note" }], TestContext.Current.CancellationToken);
        await store.Writer("run-2").WriteJsonAsync("manifest.json", new { Matches = 0 }, TestContext.Current.CancellationToken);

        (await store.DeleteAsync("run-1", first: null, TestContext.Current.CancellationToken)).ShouldBeTrue();

        Directory.Exists(Path.Combine(directory.Path, "run-1")).ShouldBeFalse();
        (await store.RunsAsync(TestContext.Current.CancellationToken)).ShouldBe(["run-2"]);
        (await store.DeleteAsync("run-1", first: null, TestContext.Current.CancellationToken)).ShouldBeFalse("it is already gone");
    }

    /// <summary>
    /// The file named first goes before the rest of the run (#818, #851): the recursive delete is made to fail, and
    /// whatever was deleted before it shows in what is left.
    /// </summary>
    [Fact]
    public async Task Deleting_a_run_deletes_the_file_named_first_before_anything_else()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path, _ => throw new IOException("The directory could not be removed."));
        await store.Writer("run-o").WriteJsonAsync("first.json", new { Revision = 1 }, TestContext.Current.CancellationToken);
        await store.Writer("run-o").WriteJsonAsync("reactions.json", new { Items = 1 }, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<IOException>(() => store.DeleteAsync("run-o", first: "first.json", TestContext.Current.CancellationToken));

        File.Exists(Path.Combine(directory.Path, "run-o", "first.json")).ShouldBeFalse();
        File.Exists(Path.Combine(directory.Path, "run-o", "reactions.json")).ShouldBeTrue();
    }

    [Fact]
    public async Task A_run_name_that_leaves_the_root_is_refused_before_anything_is_deleted()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path);

        await Should.ThrowAsync<ArgumentException>(() => store.DeleteAsync("..", first: null, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => store.DeleteAsync("a/b", first: null, TestContext.Current.CancellationToken));
    }
}
