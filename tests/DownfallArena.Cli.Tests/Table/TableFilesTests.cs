using System.Text;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// What the table host serves, held against what the page actually ships. A module that is not routed is a
/// page that dies at its first import — silently, because nothing else asks for it.
/// </summary>
public sealed class TableFilesTests
{
    private static readonly string Directory = Path.Combine(AppContext.BaseDirectory, "table");

    private static TableFiles Files => new(Directory);

    [Fact]
    public void The_page_and_its_stylesheet_are_served_at_the_paths_the_page_asks_for()
    {
        Text(Files.Get("/")).ShouldContain("<title>");
        Text(Files.Get("/index.html")).ShouldContain("table.js");
        Files.Get("/table.css").ContentType.ShouldContain("text/css");
    }

    /// <summary>
    /// By pattern rather than by name: a list with a row per file cannot notice a new one, and the day the page
    /// grows a module, this fails instead of the browser.
    /// </summary>
    [Fact]
    public void Every_module_beside_the_page_is_served()
    {
        var modules = System.IO.Directory.GetFiles(Directory, "*.js")
            .Select(Path.GetFileName)
            .Where(name => !name!.EndsWith(".test.js", StringComparison.Ordinal))
            .ToList();

        modules.ShouldNotBeEmpty();
        foreach (var module in modules)
        {
            Files.Get($"/{module}").Status.ShouldBe(200, $"{module} sits beside the page but the host does not serve it");
        }
    }

    /// <summary>A test module is not part of the page, and the host must not hand one out.</summary>
    [Fact]
    public void A_path_the_route_table_does_not_hold_is_not_read_from_disk()
    {
        Files.Get("/transport.test.js").Status.ShouldBe(404);
        Files.Get("/../appsettings.json").Status.ShouldBe(404);
        Files.Get("/favicon.ico").Status.ShouldBe(204);
    }

    private static string Text(DownfallArena.Cli.Studio.StudioResponse response) => Encoding.UTF8.GetString(response.Body);
}
