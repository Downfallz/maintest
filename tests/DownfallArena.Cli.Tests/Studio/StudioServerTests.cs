using DownfallArena.Cli.Hosting;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Evaluation;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Tests.Studio;

/// <summary>
/// The one piece of the host's routing that is worth reading on its own: which two runs a comparison path
/// names. Anything that is not exactly two is a 404 rather than a guess.
/// </summary>
public sealed class StudioServerTests
{
    [Theory]
    [InlineData("/compare/one/two", "one", "two")]
    [InlineData("/compare/one/two/", "one", "two")]
    public void A_comparison_path_names_its_two_runs(string path, string first, string second)
    {
        StudioServer.Comparison(path).ShouldBe((first, second));
    }

    [Theory]
    [InlineData("/compare/one")]
    [InlineData("/compare/one/two/three")]
    [InlineData("/compare/")]
    [InlineData("/compare")]
    [InlineData("/runs/one")]
    public void Anything_that_does_not_name_two_runs_is_not_a_comparison(string path)
    {
        StudioServer.Comparison(path).ShouldBeNull();
    }

    /// <summary>
    /// The studio saves whole content files, and the balance knobs are past the table's 64 KB: with the host's
    /// default, saving them would be answered 413.
    /// </summary>
    [Fact]
    public void The_studio_reads_a_body_as_large_as_its_largest_content_file()
    {
        using var content = new StudioContent();
        var options = new CliOptions { Command = "studio", Output = "out.csv", SchemaPath = Path.Combine(content.Path, "dst", "game.schema.json") };
        using var api = new StudioApi(
            new ContentStore(content.Path),
            new StudioRunner(options, Path.Combine(content.Path, "runs"), TimeProvider.System),
            new BenchmarkStore(Path.Combine(content.Path, "benchmarks")),
            RuleSet.Default,
            Path.Combine(content.Path, "dst"));
        var largest = Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "data"), "*.json", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}dst{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Max(file => new FileInfo(file).Length);

        using var server = new StudioServer(5101, api, new StudioFiles(content.Path, content.Path), content.Path);

        server.MaxBodyBytes.ShouldBeGreaterThan(HttpHost.DefaultMaxBodyBytes);
        server.MaxBodyBytes.ShouldBeGreaterThan((int)largest * 4, "a content file should have room to grow before the studio refuses to save it");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DownfallArena.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found above the test binaries.");
    }
}
