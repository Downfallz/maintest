using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Tests.Resources;

namespace DownfallArena.Infrastructure.Tests.Learning;

public sealed class FileArtifactReaderTests
{
    [Fact]
    public async Task What_the_writer_wrote_reads_back_by_the_same_path()
    {
        using var directory = new ContentDirectory();
        var store = new FileArtifactStore(directory.Path);
        await store.Writer("one").WriteJsonAsync("manifest.json", new { Matches = 3 }, TestContext.Current.CancellationToken);

        var text = await store.Reader("one").ReadTextAsync("manifest.json", TestContext.Current.CancellationToken);

        text.ShouldNotBeNull().ShouldContain("\"matches\": 3");
    }

    [Fact]
    public async Task A_path_nobody_wrote_reads_as_nothing()
    {
        using var directory = new ContentDirectory();
        var reader = new FileArtifactReader(directory.Path);

        (await reader.ReadTextAsync("manifest.json", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await reader.ListAsync("traces", TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Listing_a_directory_gives_its_files_as_relative_paths_in_order()
    {
        using var directory = new ContentDirectory();
        var writer = new FileArtifactWriter(directory.Path);
        await writer.WriteJsonAsync("traces/b.json", new { }, TestContext.Current.CancellationToken);
        await writer.WriteJsonAsync("traces/a.json", new { }, TestContext.Current.CancellationToken);

        var listed = await new FileArtifactReader(directory.Path).ListAsync("traces", TestContext.Current.CancellationToken);

        listed.ShouldBe(["traces/a.json", "traces/b.json"]);
    }

    [Theory]
    [InlineData("../elsewhere.json")]
    [InlineData("/etc/passwd")]
    public async Task A_path_that_leaves_the_run_is_refused(string path)
    {
        using var directory = new ContentDirectory();
        var reader = new FileArtifactReader(directory.Path);

        await Should.ThrowAsync<ArgumentException>(() => reader.ReadTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("..")]
    public void A_run_is_one_directory_under_the_store(string run)
    {
        using var directory = new ContentDirectory();

        Should.Throw<ArgumentException>(() => new FileArtifactStore(directory.Path).LocationOf(run));
    }

    [Fact]
    public void A_run_is_located_under_the_root()
    {
        using var directory = new ContentDirectory();

        new FileArtifactStore(directory.Path).LocationOf("20260927-1200-ab12").ShouldBe(Path.Combine(Path.GetFullPath(directory.Path), "20260927-1200-ab12"));
    }
}
