using System.Text.Json;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Tests.Studio;

/// <summary>
/// <see cref="ViewerPage"/> carries a run's artifacts into the viewer by matching two literals from
/// <c>viewer/index.html</c>, one of them indentation-sensitive. These run against the real file, so re-indenting
/// the viewer fails here instead of turning every studio run page into a 404.
/// </summary>
public sealed class ViewerPageTests
{
    private static string Viewer => Path.Combine(AppContext.BaseDirectory, "viewer");

    [Fact]
    public void A_run_page_is_the_viewer_with_its_stylesheet_inlined_and_its_artifacts_carried()
    {
        var page = ViewerPage.Render(Viewer, "20260909-120000-match-1", [("match.trace.json", "{\"matchId\":\"m\",\"entries\":[]}")]);

        page.ShouldNotContain("<link rel=\"stylesheet\" href=\"viewer.css\">");
        page.ShouldContain("<style>");
        page.ShouldContain("id=\"embedded-artifacts\"");
        page.ShouldContain("'use strict';");
    }

    [Fact]
    public void The_carried_block_is_the_json_the_viewer_reads_at_start()
    {
        var page = ViewerPage.Render(Viewer, "run-1", [("evaluation.json", "{\"agentA\":\"Greedy\"}")]);

        using var carried = JsonDocument.Parse(Between(page, "type=\"application/json\">", "</script>"));
        carried.RootElement.GetProperty("run").GetString().ShouldBe("run-1");
        var artifact = carried.RootElement.GetProperty("artifacts").EnumerateArray().ShouldHaveSingleItem();
        artifact.GetProperty("name").GetString().ShouldBe("evaluation.json");
        artifact.GetProperty("path").GetString().ShouldBe("run-1/evaluation.json");
        artifact.GetProperty("text").GetString().ShouldBe("{\"agentA\":\"Greedy\"}");
    }

    /// <summary>A closing tag inside the JSON would end the script element early and lose the rest of the page.</summary>
    [Fact]
    public void A_closing_tag_inside_an_artifact_does_not_end_the_block_early()
    {
        var page = ViewerPage.Render(Viewer, "run-1", [("trace.json", "{\"note\":\"</script><b>hi</b>\"}")]);

        page.ShouldNotContain("</script><b>hi</b>");
        var text = JsonDocument.Parse(Between(page, "type=\"application/json\">", "</script>"))
            .RootElement.GetProperty("artifacts").EnumerateArray().Single().GetProperty("text").GetString();
        text.ShouldBe("{\"note\":\"</script><b>hi</b>\"}");
    }

    [Fact]
    public void A_directory_that_is_not_the_viewer_says_so_rather_than_producing_a_broken_page()
    {
        Should.Throw<FileNotFoundException>(() => ViewerPage.Render(Path.Combine(AppContext.BaseDirectory, "nowhere"), "run-1", []));
        Should.Throw<InvalidDataException>(() => ViewerPage.Render(NotTheViewer(), "run-1", []));
    }

    /// <summary>A directory holding the two file names the viewer has, but not the viewer.</summary>
    private static string NotTheViewer()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "scratch", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "index.html"), "<html><body>not the viewer</body></html>");
        File.WriteAllText(Path.Combine(directory, "viewer.css"), "body { color: red; }");
        return directory;
    }

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal) + start.Length;
        return text[from..text.IndexOf(end, from, StringComparison.Ordinal)];
    }
}
