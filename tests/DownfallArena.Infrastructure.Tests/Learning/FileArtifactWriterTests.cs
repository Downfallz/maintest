using System.Text.Json;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Tests.Resources;

namespace DownfallArena.Infrastructure.Tests.Learning;

public sealed class FileArtifactWriterTests
{
    [Fact]
    public async Task A_document_is_written_indented_in_camel_case_under_the_root()
    {
        using var directory = new ContentDirectory();
        var writer = new FileArtifactWriter(directory.Path);

        await writer.WriteJsonAsync("traces/one.json", new { MatchCount = 2, Name = "run" }, TestContext.Current.CancellationToken);

        var path = Path.Combine(directory.Path, "traces", "one.json");
        File.Exists(path).ShouldBeTrue();
        var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        text.ShouldContain("\n");
        using var document = JsonDocument.Parse(text);
        document.RootElement.GetProperty("matchCount").GetInt32().ShouldBe(2);
        document.RootElement.GetProperty("name").GetString().ShouldBe("run");
        writer.RootDirectory.ShouldBe(Path.GetFullPath(directory.Path));
    }

    [Fact]
    public async Task Writing_a_document_again_replaces_it()
    {
        using var directory = new ContentDirectory();
        var writer = new FileArtifactWriter(directory.Path);

        await writer.WriteJsonAsync("manifest.json", new { Matches = 0 }, TestContext.Current.CancellationToken);
        await writer.WriteJsonAsync("manifest.json", new { Matches = 3 }, TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory.Path, "manifest.json"), TestContext.Current.CancellationToken));
        document.RootElement.GetProperty("matches").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task Lines_are_appended_one_compact_value_per_line()
    {
        using var directory = new ContentDirectory();
        var writer = new FileArtifactWriter(directory.Path);

        await writer.AppendJsonLinesAsync("steps.jsonl", new[] { new { Index = 0 }, new { Index = 1 } }, TestContext.Current.CancellationToken);
        await writer.AppendJsonLinesAsync("steps.jsonl", new[] { new { Index = 2 } }, TestContext.Current.CancellationToken);
        await writer.AppendJsonLinesAsync("steps.jsonl", Array.Empty<object>(), TestContext.Current.CancellationToken);

        var lines = await File.ReadAllLinesAsync(Path.Combine(directory.Path, "steps.jsonl"), TestContext.Current.CancellationToken);
        lines.ShouldBe(["""{"index":0}""", """{"index":1}""", """{"index":2}"""]);
    }

    [Fact]
    public async Task Paths_that_leave_the_root_and_empty_values_are_refused()
    {
        using var directory = new ContentDirectory();
        var writer = new FileArtifactWriter(directory.Path);
        var rooted = Path.Combine(Path.GetTempPath(), "outside.json");

        await Should.ThrowAsync<ArgumentException>(() => writer.WriteJsonAsync("../outside.json", new { }, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => writer.WriteJsonAsync(rooted, new { }, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => writer.AppendJsonLinesAsync(" ", Array.Empty<object>(), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => writer.WriteJsonAsync<object>("a.json", null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => writer.AppendJsonLinesAsync<object>("a.jsonl", null!, TestContext.Current.CancellationToken));
        Should.Throw<ArgumentException>(() => new FileArtifactWriter(" "));
        Directory.EnumerateFileSystemEntries(directory.Path).ShouldBeEmpty();
    }
}
