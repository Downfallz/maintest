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
    [InlineData("/backend.js", "export function hostedBackend(")]
    [InlineData("/github.js", "export function githubBackend(")]
    [InlineData("/studio.css", ".banner")]
    [InlineData("/viewer.css", "--ink")]
    public void Every_file_the_page_asks_for_is_served(string path, string expected)
    {
        var response = Files.Get(path);

        response.Status.ShouldBe(200);
        System.Text.Encoding.UTF8.GetString(response.Body).ShouldContain(expected);
    }

    /// <summary>
    /// Every module beside the page is routed, and every module the page publishes is copied by the workflow. A
    /// list with a row per file cannot notice a new one, and a module that is not served is a page that dies at
    /// its first import -- silently, because nothing else asks for it.
    /// </summary>
    [Fact]
    public void Every_module_in_the_studio_directory_is_both_served_and_published()
    {
        var modules = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "studio"), "*.js")
            .Select(Path.GetFileName)
            .Where(name => !name!.EndsWith(".test.js", StringComparison.Ordinal))
            .ToList();
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "pages.yml"));

        modules.ShouldNotBeEmpty();
        foreach (var module in modules)
        {
            Files.Get($"/{module}").Status.ShouldBe(200, $"{module} sits beside the page but the host does not serve it");
            workflow.ShouldContain($"studio/{module}", Case.Sensitive, $"{module} is served but pages.yml never copies it");
        }
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
