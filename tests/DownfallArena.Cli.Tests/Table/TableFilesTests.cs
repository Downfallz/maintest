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

    /// <summary>
    /// Discord's return (sign-in methods plan, P3) at the address its app lists, naming its files from the root: a page
    /// under <c>/signin/</c> would read a relative <c>table.css</c> as <c>/signin/table.css</c>.
    /// </summary>
    [Fact]
    public void Discord_returns_to_a_page_that_names_its_files_from_the_root()
    {
        var page = Text(Files.Get("/signin/discord"));

        page.ShouldContain("src=\"/signin.js?v=");
        page.ShouldContain("href=\"/identity.css?v=");
        page.ShouldContain("<meta name=\"referrer\" content=\"no-referrer\" />");
        Files.Get("/discord.js").Status.ShouldBe(200);
    }

    /// <summary>What a phone installs and shows (ADR 0118): the worker at the root, the manifest as a manifest, the icon as bytes.</summary>
    [Fact]
    public void The_worker_the_manifest_and_the_icons_are_served_as_what_they_are()
    {
        Files.Get("/sw.js").ContentType.ShouldContain("text/javascript");
        Files.Get("/manifest.webmanifest").ContentType.ShouldBe("application/manifest+json");
        Files.Get("/icon.svg").ContentType.ShouldBe("image/svg+xml");
        Files.Get("/brand-mark.svg").ContentType.ShouldBe("image/svg+xml");
        var bitmap = Files.Get("/icon-192.png");
        bitmap.ContentType.ShouldBe("image/png");
        bitmap.Body.Take(4).ShouldBe([0x89, (byte)'P', (byte)'N', (byte)'G']);
        Files.Get("/icon-512.png").Status.ShouldBe(200);
        Files.Get("/icon-maskable-192.png").Status.ShouldBe(200);
        Files.Get("/icon-maskable-512.png").Status.ShouldBe(200);
        // The worker names the badge Android masks into the status bar; unrouted, every push falls back to a blank one.
        var badge = Files.Get("/notification-badge.png");
        badge.ContentType.ShouldBe("image/png");
        badge.Body.Take(4).ShouldBe([0x89, (byte)'P', (byte)'N', (byte)'G']);
    }

    /// <summary>The display face is the host's own file (ADR 0158): no page asks another site for a font.</summary>
    [Fact]
    public void The_display_face_is_served_as_woff2_bytes()
    {
        var font = Files.Get("/fonts/marcellus-latin-400.woff2");

        font.Status.ShouldBe(200);
        font.ContentType.ShouldBe("font/woff2");
        Encoding.ASCII.GetString(font.Body, 0, 4).ShouldBe("wOF2");

        // Read as text, a font comes back longer than its file and no browser can use it.
        font.Body.Length.ShouldBe(14552);
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
    /// The official rulebook (ADR 0188, rulebook-plan.md R3) is a page anyone may read, signed in or not: the page,
    /// its own script and style, and the book it reads, served as JSON and revalidated like every page.
    /// </summary>
    [Fact]
    public void The_rulebook_is_served_with_its_script_its_style_and_the_book_it_reads()
    {
        var page = Files.Get("/rules");
        var book = Files.Get("/rules.json");

        page.Status.ShouldBe(200);
        page.ContentType.ShouldBe(DownfallArena.Cli.Studio.StudioResponse.Html);
        Text(page).ShouldContain("rules.js");
        Files.Get("/rules.js").Status.ShouldBe(200);
        Files.Get("/rules.css").ContentType.ShouldContain("text/css");
        book.Status.ShouldBe(200);
        book.ContentType.ShouldBe(DownfallArena.Cli.Studio.StudioResponse.Json);
        Text(book).ShouldContain("\"parts\"");
        book.Headers.ShouldNotBeNull().ShouldContain(new KeyValuePair<string, string>("Cache-Control", TableFiles.RevalidatedCache));
    }

    /// <summary>The rulebook's French edition (rulebook-plan.md R6) is served beside the English, as JSON.</summary>
    [Fact]
    public void The_rulebook_s_french_edition_is_served_beside_the_english()
    {
        var book = Files.Get("/rules.fr.json");

        book.Status.ShouldBe(200);
        book.ContentType.ShouldBe(DownfallArena.Cli.Studio.StudioResponse.Json);
        Text(book).ShouldContain("\"language\": \"fr\"");
        book.Headers.ShouldNotBeNull().ShouldContain(new KeyValuePair<string, string>("Cache-Control", TableFiles.RevalidatedCache));
    }

    /// <summary>The admin panel's tabs are modules its own module imports (ADR 0174, A3), served beside it.</summary>
    [Theory]
    [InlineData("/admin-players.js")]
    [InlineData("/admin-jobs.js")]
    [InlineData("/admin-turns.js")]
    [InlineData("/admin-notices.js")]
    [InlineData("/admin-void.js")]
    [InlineData("/admin-settings.js")]
    [InlineData("/admin-overview.js")]
    [InlineData("/admin-activity.js")]
    [InlineData("/admin-feedback.js")]
    public void The_admin_panel_s_tab_modules_are_served(string module)
    {
        Files.Get(module).Status.ShouldBe(200);
    }

    /// <summary>
    /// The battle laboratory (ADR 0123) is a page of this host like the others, and what it loads is relative to
    /// itself: the stylesheet and the bundle it names resolve to routes of the host.
    /// </summary>
    [Fact]
    public void The_battle_laboratory_is_served_with_the_stylesheet_and_the_bundle_it_names()
    {
        var page = Files.Get("/battle-lab");

        page.Status.ShouldBe(200);
        page.ContentType.ShouldContain("text/html");
        Text(page).ShouldContain("href=\"battle-lab.css?v=");
        Text(page).ShouldContain("src=\"battle-build/game.js?v=");
        Files.Get("/battle-lab.css").ContentType.ShouldContain("text/css");
        Files.Get("/battle-build/game.js").ContentType.ShouldContain("text/javascript");
    }

    [Fact]
    public void The_live_battle_loads_its_own_bundle_beside_the_shared_art_and_styles()
    {
        Files.Get("/battle-preferences.js").ContentType.ShouldContain("text/javascript");
        var page = Files.Get("/battle");

        page.Status.ShouldBe(200);
        Text(page).ShouldContain("src=\"battle-build/live.js?v=");
        Text(page).ShouldContain("href=\"battle.css?v=");
        Files.Get("/battle.css").ContentType.ShouldContain("text/css");
        Files.Get("/battle-services.js").ContentType.ShouldContain("text/javascript");
        Files.Get("/battle-inspection.js").ContentType.ShouldContain("text/javascript");
        Text(page).ShouldContain("id=\"inspect-body\"");
        Files.Get("/battle-order.js").ContentType.ShouldContain("text/javascript");
        Text(page).ShouldContain("id=\"order-dialog\"");
        Files.Get("/battle-build/live.js").ContentType.ShouldContain("text/javascript");
    }

    /// <summary>
    /// The bundle is built, not committed: a route to a file that is not there answers 500 and says so, and this
    /// test's own failure says what to run.
    /// </summary>
    [Fact]
    public void The_battle_bundle_and_the_notices_built_beside_it_are_served()
    {
        const string hint = "Run `npm --prefix battle ci --ignore-scripts && npm --prefix battle run build` first";

        foreach (var file in new[] { "game.js", "game.js.LEGAL.txt", "live.js", "live.js.LEGAL.txt", "fx.js", "PHASER-LICENSE.txt" })
        {
            var served = Files.Get($"/battle-build/{file}");
            served.Status.ShouldBe(200, $"battle-build/{file} is not served. {hint}");
        }

        Text(Files.Get("/battle-build/game.js.LEGAL.txt")).ShouldContain("Phaser");
        Files.Get("/battle-build/PHASER-LICENSE.txt").ContentType.ShouldContain("text/plain");
    }

    /// <summary>The scene's art is bytes, served as the image it is rather than decoded as text.</summary>
    [Theory]
    [InlineData("eclipse-battle")]
    [InlineData("horizon/fallen-ledge")]
    [InlineData("horizon/breach")]
    [InlineData("horizon/shattered-sky")]
    [InlineData("horizon/broken-floor")]
    [InlineData("creatures")]
    [InlineData("base-creature")]
    [InlineData("brute")]
    [InlineData("predator")]
    [InlineData("warped")]
    public void The_battle_art_is_served_as_webp_bytes(string asset)
    {
        var image = Files.Get($"/battle-art/{asset}.webp");

        image.Status.ShouldBe(200);
        image.ContentType.ShouldBe("image/webp");
        Encoding.ASCII.GetString(image.Body, 0, 4).ShouldBe("RIFF");
        Encoding.ASCII.GetString(image.Body, 8, 4).ShouldBe("WEBP");
    }

    /// <summary>
    /// A page's pictures at the size they are drawn (ADR 0198): every file the kit's module names is served as the
    /// image it is, kept a day like any picture, and the host serves no other from that folder.
    /// </summary>
    [Fact]
    public void Every_picture_the_kit_names_is_served_as_webp_bytes()
    {
        // page-art.js writes every page's entries but the battle's, which it imports from battle-page-art.js (#1016).
        var module = Encoding.UTF8.GetString(Files.Get("/page-art.js").Body) + Encoding.UTF8.GetString(Files.Get("/battle-page-art.js").Body);
        var named = module.Split("file: '").Skip(1).Select(rest => rest[..rest.IndexOf('\'', StringComparison.Ordinal)])
            .Order(StringComparer.Ordinal).ToList();

        var served = Files.Routes.Where(route => route.StartsWith("/page-art/", StringComparison.Ordinal))
            .Select(route => route["/page-art/".Length..]).Order(StringComparer.Ordinal).ToList();

        named.ShouldNotBeEmpty();
        served.ShouldBe(named);
        foreach (var file in named)
        {
            var image = Files.Get($"/page-art/{file}");
            image.ContentType.ShouldBe("image/webp");
            Encoding.ASCII.GetString(image.Body, 8, 4).ShouldBe("WEBP");
            image.Headers.ShouldNotBeNull().ShouldContain(header => header.Key == "Cache-Control" && header.Value == TableFiles.LastingCache);
        }
    }

    /// <summary>
    /// The battle's windows draw a spell's portrait at 24 px and a mutation's form at 64 px (UX12 P2): both are
    /// served at that size and twice it, beside the 40 px portrait a card draws.
    /// </summary>
    [Theory]
    [InlineData("spell-strike", 24)]
    [InlineData("spell-strike", 48)]
    [InlineData("spell-strike", 40)]
    [InlineData("spell-strike", 80)]
    [InlineData("mutation-brute-base", 64)]
    [InlineData("mutation-brute-base", 128)]
    public void The_windows_pictures_are_served_at_the_size_they_are_drawn(string picture, int width)
    {
        var prefix = $"/page-art/{picture}-{width}.";
        var route = Files.Routes.SingleOrDefault(route => route.StartsWith(prefix, StringComparison.Ordinal));

        route.ShouldNotBeNull($"{picture} is served at {width} px");
        Files.Get(route).ContentType.ShouldBe("image/webp");
    }

    /// <summary>
    /// Every evolution illustration the page can ask for is served as the image it is, and none is left out: the
    /// route table names each file of the folder and of its hybrids (ADR 0151, ADR 0152).
    /// </summary>
    [Fact]
    public void Every_evolution_illustration_is_served_as_webp_bytes()
    {
        var folder = Path.Combine(Directory, "battle-art", "evolutions");
        var files = System.IO.Directory.GetFiles(folder, "*.webp", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/')).Order(StringComparer.Ordinal).ToList();

        var served = Files.Routes.Where(route => route.StartsWith("/battle-art/evolutions/", StringComparison.Ordinal))
            .Select(route => route["/battle-art/evolutions/".Length..]).Order(StringComparer.Ordinal).ToList();

        served.ShouldBe(files);
        files.ShouldAllBe(file => Encoding.ASCII.GetString(Files.Get($"/battle-art/evolutions/{file}").Body, 8, 4) == "WEBP");
    }

    /// <summary>
    /// The battle's page names its bundle with the bundle's own tag, so a cache that kept yesterday's script cannot pair it with
    /// today's page: the battle died at load on "Connecting…" when it did, after an element was renamed.
    /// </summary>
    [Fact]
    public void A_page_names_its_scripts_and_styles_by_their_tags()
    {
        var bundle = Files.Get("/battle-build/live.js").Headers!.Single(header => header.Key == "ETag").Value;

        var page = Text(Files.Get("/battle"));

        page.ShouldContain($"src=\"battle-build/live.js?v={bundle[1..13]}\"");
        page.ShouldNotContain("src=\"battle-build/live.js\"");
    }

    /// <summary>
    /// A page's own modules are named by the release's tag, in the page and in every module that imports one, so
    /// the address is one and the browser runs each module once: `ways.js` of one deploy importing `account.js` of the one
    /// before refused to load, and the return from Discord stayed on "Checking your sign-in…".
    /// </summary>
    [Fact]
    public void A_page_and_its_modules_name_every_module_by_one_release_tag()
    {
        var page = Text(Files.Get("/signin/discord"));
        var release = System.Text.RegularExpressions.Regex.Match(page, "src=\"/signin\\.js\\?v=([0-9a-f]{12})\"").Groups[1].Value;

        release.ShouldNotBeEmpty();
        var signIn = Text(Files.Get("/signin.js"));
        signIn.ShouldContain($"from './account.js?v={release}'");
        signIn.ShouldContain($"from './ways.js?v={release}'");
        Text(Files.Get("/ways.js")).ShouldContain($"from './account.js?v={release}'");
        Text(Files.Get("/")).ShouldContain($"src=\"navigation.js?v={release}\"");
        Text(Files.Get("/rules.js")).ShouldContain($"import('./picture.js?v={release}')", customMessage: "a module loaded later is the page's own copy");
    }

    /// <summary>One module changed is a new release: the page and a module that imports another, unchanged, both name it.</summary>
    [Fact]
    public void A_changed_module_renames_every_module_in_the_page_and_its_imports()
    {
        var folder = System.IO.Directory.CreateTempSubdirectory("table-files-").FullName;
        File.WriteAllText(Path.Combine(folder, "signin.html"), "<script type=\"module\" src=\"/signin.js\"></script>");
        File.WriteAllText(Path.Combine(folder, "signin.js"), "import { a } from './account.js';\nimport './ways.js';\nimport { b } from './elsewhere.js';");
        File.WriteAllText(Path.Combine(folder, "account.js"), "export const a = 1;");
        var files = new TableFiles(folder);
        var page = Text(files.Get("/signin/discord"));
        var module = Text(files.Get("/signin.js"));

        File.WriteAllText(Path.Combine(folder, "account.js"), "export const a = 2;");
        File.SetLastWriteTimeUtc(Path.Combine(folder, "account.js"), DateTime.UtcNow.AddMinutes(1));

        Text(files.Get("/signin/discord")).ShouldNotBe(page);
        var after = Text(files.Get("/signin.js"));
        after.ShouldNotBe(module);
        after.ShouldMatch("from './account\\.js\\?v=[0-9a-f]{12}';\\nimport './ways\\.js\\?v=[0-9a-f]{12}';");
        after.ShouldContain("from './elsewhere.js';", customMessage: "a name the host does not serve is left as it is");
        System.IO.Directory.Delete(folder, recursive: true);
    }

    /// <summary>The worker is registered at one address, so neither its name nor its text is stamped.</summary>
    [Fact]
    public void The_worker_keeps_its_bare_name_and_its_text()
    {
        Text(Files.Get("/sw.js")).ShouldBe(File.ReadAllText(Path.Combine(Directory, "sw.js")));
        Text(Files.Get("/lobby")).ShouldNotContain("sw.js?v=");
    }

    /// <summary>A new bundle is a new page: its names change, and so does its tag, so a browser's 304 cannot keep the old one.</summary>
    [Fact]
    public void A_changed_bundle_changes_the_page_that_names_it()
    {
        var folder = System.IO.Directory.CreateTempSubdirectory("table-files-").FullName;
        System.IO.Directory.CreateDirectory(Path.Combine(folder, "battle-build"));
        File.WriteAllText(Path.Combine(folder, "battle.html"), "<script type=\"module\" src=\"battle-build/live.js\"></script><link rel=\"stylesheet\" href=\"https://elsewhere.invalid/x.css\">");
        File.WriteAllText(Path.Combine(folder, "battle-build", "live.js"), "export const deploy = 1;");
        var files = new TableFiles(folder);
        var before = files.Get("/battle");

        File.WriteAllText(Path.Combine(folder, "battle-build", "live.js"), "export const deploy = 2;");
        File.SetLastWriteTimeUtc(Path.Combine(folder, "battle-build", "live.js"), DateTime.UtcNow.AddMinutes(1));
        var after = files.Get("/battle");

        Text(after).ShouldNotBe(Text(before));
        Text(after).ShouldContain("href=\"https://elsewhere.invalid/x.css\"");
        after.Headers!.Single(header => header.Key == "ETag").Value.ShouldNotBe(before.Headers!.Single(header => header.Key == "ETag").Value);
        System.IO.Directory.Delete(folder, recursive: true);
    }

    /// <summary>A test module is not part of the page, and the host must not hand one out.</summary>
    [Fact]
    public void A_path_the_route_table_does_not_hold_is_not_read_from_disk()
    {
        Files.Get("/transport.test.js").Status.ShouldBe(404);
        Files.Get("/battle-lab.test.js").Status.ShouldBe(404);
        Files.Get("/../appsettings.json").Status.ShouldBe(404);
        Files.Get("/favicon.ico").Status.ShouldBe(204);
    }

    private static string Text(DownfallArena.Cli.Studio.StudioResponse response) => Encoding.UTF8.GetString(response.Body);
}
