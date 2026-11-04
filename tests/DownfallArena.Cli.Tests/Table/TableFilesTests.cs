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

    /// <summary>What a phone installs and shows (ADR 0118): the worker at the root, the manifest as a manifest, the icon as bytes.</summary>
    [Fact]
    public void The_worker_the_manifest_and_the_icons_are_served_as_what_they_are()
    {
        Files.Get("/sw.js").ContentType.ShouldContain("text/javascript");
        Files.Get("/manifest.webmanifest").ContentType.ShouldBe("application/manifest+json");
        Files.Get("/icon.svg").ContentType.ShouldBe("image/svg+xml");
        var bitmap = Files.Get("/icon-192.png");
        bitmap.ContentType.ShouldBe("image/png");
        bitmap.Body.Take(4).ShouldBe([0x89, (byte)'P', (byte)'N', (byte)'G']);
        Files.Get("/icon-512.png").Status.ShouldBe(200);
    }

    /// <summary>The operator's pages are routes of this host too (ADR 0081), reached by name rather than by file.</summary>
    [Theory]
    [InlineData("/pilot", "pilot.js")]
    [InlineData("/admin", "admin.js")]
    public void The_operator_s_pages_are_served_with_their_own_modules(string page, string module)
    {
        var served = Files.Get(page);

        served.Status.ShouldBe(200);
        Text(served).ShouldContain(module);
        Files.Get($"/{module}").Status.ShouldBe(200);
    }

    /// <summary>
    /// The arena laboratory (ADR 0123) is a page of this host like the others, and what it loads is relative to
    /// itself: the stylesheet and the bundle it names resolve to routes of the host.
    /// </summary>
    [Fact]
    public void The_arena_laboratory_is_served_with_the_stylesheet_and_the_bundle_it_names()
    {
        var page = Files.Get("/arena-lab");

        page.Status.ShouldBe(200);
        page.ContentType.ShouldContain("text/html");
        Text(page).ShouldContain("href=\"arena-lab.css\"");
        Text(page).ShouldContain("src=\"arena-build/game.js\"");
        Files.Get("/arena-lab.css").ContentType.ShouldContain("text/css");
        Files.Get("/arena-build/game.js").ContentType.ShouldContain("text/javascript");
    }

    [Fact]
    public void The_live_arena_loads_its_own_bundle_beside_the_shared_art_and_styles()
    {
        var page = Files.Get("/arena");

        page.Status.ShouldBe(200);
        Text(page).ShouldContain("src=\"arena-build/live.js\"");
        Text(page).ShouldContain("href=\"arena.css\"");
        Files.Get("/arena.css").ContentType.ShouldContain("text/css");
        Files.Get("/arena-build/live.js").ContentType.ShouldContain("text/javascript");
    }

    /// <summary>
    /// The bundle is built, not committed: a route to a file that is not there answers 500 and says so, and this
    /// test's own failure says what to run.
    /// </summary>
    [Fact]
    public void The_arena_bundle_and_the_notices_built_beside_it_are_served()
    {
        const string hint = "Run `npm --prefix arena ci --ignore-scripts && npm --prefix arena run build` first";

        foreach (var file in new[] { "game.js", "game.js.LEGAL.txt", "live.js", "live.js.LEGAL.txt", "PHASER-LICENSE.txt" })
        {
            var served = Files.Get($"/arena-build/{file}");
            served.Status.ShouldBe(200, $"arena-build/{file} is not served. {hint}");
        }

        Text(Files.Get("/arena-build/game.js.LEGAL.txt")).ShouldContain("Phaser");
        Files.Get("/arena-build/PHASER-LICENSE.txt").ContentType.ShouldContain("text/plain");
    }

    /// <summary>The scene's art is bytes, served as the image it is rather than decoded as text.</summary>
    [Theory]
    [InlineData("eclipse-arena")]
    [InlineData("base-creature")]
    [InlineData("brute")]
    [InlineData("predator")]
    [InlineData("warped")]
    public void The_arena_art_is_served_as_webp_bytes(string asset)
    {
        var image = Files.Get($"/arena-art/{asset}.webp");

        image.Status.ShouldBe(200);
        image.ContentType.ShouldBe("image/webp");
        Encoding.ASCII.GetString(image.Body, 0, 4).ShouldBe("RIFF");
        Encoding.ASCII.GetString(image.Body, 8, 4).ShouldBe("WEBP");
    }

    /// <summary>A test module is not part of the page, and the host must not hand one out.</summary>
    [Fact]
    public void A_path_the_route_table_does_not_hold_is_not_read_from_disk()
    {
        Files.Get("/transport.test.js").Status.ShouldBe(404);
        Files.Get("/arena-lab.test.js").Status.ShouldBe(404);
        Files.Get("/../appsettings.json").Status.ShouldBe(404);
        Files.Get("/favicon.ico").Status.ShouldBe(204);
    }

    private static string Text(DownfallArena.Cli.Studio.StudioResponse response) => Encoding.UTF8.GetString(response.Body);
}
