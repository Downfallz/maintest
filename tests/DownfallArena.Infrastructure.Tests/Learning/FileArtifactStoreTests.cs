using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Tests.Resources;

namespace DownfallArena.Infrastructure.Tests.Learning;

/// <summary>The store over a root directory: a run is one directory under it, listed and removed as such.</summary>
public sealed class FileArtifactStoreTests
{
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
