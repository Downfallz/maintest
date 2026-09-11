using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Tests.Studio;

/// <summary>
/// The static routes are a fixed table, not a path lookup, so no request can name a file the studio does not
/// ship. These run against the real <c>studio/</c> and <c>viewer/</c> files copied next to the tests, which is
/// what makes them catch a module the page imports and the table does not serve: the page is a blank screen
/// then, and nothing else would say so.
/// </summary>
public sealed class StudioFilesTests
{
    private static StudioFiles Files => new(
        Path.Combine(AppContext.BaseDirectory, "studio"),
        Path.Combine(AppContext.BaseDirectory, "viewer"));

    [Theory]
    [InlineData("/", "Downfall Arena content studio")]
    [InlineData("/index.html", "<script type=\"module\" src=\"studio.js\">")]
    [InlineData("/studio.js", "import { backendForThisPage } from './backend.js';")]
    [InlineData("/backend.js", "/api/catalogue")]
    [InlineData("/backend.js", "export function hostedBackend()")]
    [InlineData("/studio.css", ".banner")]
    [InlineData("/viewer.css", "--ink")]
    public void Every_file_the_page_asks_for_is_served(string path, string expected)
    {
        var response = Files.Get(path);

        response.Status.ShouldBe(200);
        System.Text.Encoding.UTF8.GetString(response.Body).ShouldContain(expected);
    }

    /// <summary>The browser asks for an icon the studio does not ship; a 404 would be a console error on every load.</summary>
    [Fact]
    public void The_icon_the_browser_asks_for_on_its_own_is_answered_empty()
    {
        var response = Files.Get("/favicon.ico");

        response.Status.ShouldBe(204);
        response.Body.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/../Directory.Build.props")]
    [InlineData("/studio/studio.js")]
    [InlineData("/appsettings.json")]
    [InlineData("/api/catalogue")]
    public void Anything_else_is_a_404(string path)
    {
        Files.Get(path).Status.ShouldBe(404);
    }
}
