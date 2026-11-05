using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The static files the table serves, as a fixed route table. Nothing is derived from the request path, so the
/// host cannot be talked into reading a file the table does not ship.
/// </summary>
/// <remarks>
/// It answers with <see cref="StudioResponse" /> because the two hosts of this CLI give the same kind of
/// answer; the type is named after the first host that needed it rather than after the pair.
/// </remarks>
internal sealed class TableFiles
{
    private const string JavaScript = "text/javascript; charset=utf-8";

    private const string Css = "text/css; charset=utf-8";

    private const string Manifest = "application/manifest+json";

    private const string Svg = "image/svg+xml";

    private const string Png = "image/png";

    private const string Webp = "image/webp";

    private readonly Dictionary<string, (string Path, string ContentType)> _routes;

    public TableFiles(string tableDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableDirectory);

        var index = (Path.Combine(tableDirectory, "index.html"), StudioResponse.Html);
        _routes = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["/"] = index,
            ["/index.html"] = index,
            ["/play"] = index,
            ["/lobby"] = (Path.Combine(tableDirectory, "lobby.html"), StudioResponse.Html),
            ["/lobby.js"] = (Path.Combine(tableDirectory, "lobby.js"), JavaScript),
            ["/account.js"] = (Path.Combine(tableDirectory, "account.js"), JavaScript),
            ["/devices.js"] = (Path.Combine(tableDirectory, "devices.js"), JavaScript),
            ["/friends.js"] = (Path.Combine(tableDirectory, "friends.js"), JavaScript),
            ["/players.js"] = (Path.Combine(tableDirectory, "players.js"), JavaScript),
            ["/leaderboard"] = (Path.Combine(tableDirectory, "leaderboard.html"), StudioResponse.Html),
            ["/leaderboard.js"] = (Path.Combine(tableDirectory, "leaderboard.js"), JavaScript),
            ["/leaderboard.css"] = (Path.Combine(tableDirectory, "leaderboard.css"), Css),
            ["/lobby.css"] = (Path.Combine(tableDirectory, "lobby.css"), Css),
            ["/table.css"] = (Path.Combine(tableDirectory, "table.css"), Css),
            ["/table.js"] = (Path.Combine(tableDirectory, "table.js"), JavaScript),
            ["/transport.js"] = (Path.Combine(tableDirectory, "transport.js"), JavaScript),
            ["/seats.js"] = (Path.Combine(tableDirectory, "seats.js"), JavaScript),
            ["/session.js"] = (Path.Combine(tableDirectory, "session.js"), JavaScript),
            ["/invitation.js"] = (Path.Combine(tableDirectory, "invitation.js"), JavaScript),
            ["/outcome.js"] = (Path.Combine(tableDirectory, "outcome.js"), JavaScript),
            ["/push.js"] = (Path.Combine(tableDirectory, "push.js"), JavaScript),

            // The service worker, at the root so its scope is the whole table (ADR 0118), and what an installed
            // page and a notification show: the manifest and the icon, as a vector and as the two bitmaps
            // Android asks for.
            ["/sw.js"] = (Path.Combine(tableDirectory, "sw.js"), JavaScript),
            ["/manifest.webmanifest"] = (Path.Combine(tableDirectory, "manifest.webmanifest"), Manifest),
            ["/icon.svg"] = (Path.Combine(tableDirectory, "icon.svg"), Svg),
            ["/icon-192.png"] = (Path.Combine(tableDirectory, "icon-192.png"), Png),
            ["/icon-512.png"] = (Path.Combine(tableDirectory, "icon-512.png"), Png),
            ["/card.js"] = (Path.Combine(tableDirectory, "card.js"), JavaScript),
            ["/board.js"] = (Path.Combine(tableDirectory, "board.js"), JavaScript),
            ["/timeline.js"] = (Path.Combine(tableDirectory, "timeline.js"), JavaScript),
            ["/ties.js"] = (Path.Combine(tableDirectory, "ties.js"), JavaScript),
            ["/mat.js"] = (Path.Combine(tableDirectory, "mat.js"), JavaScript),
            ["/hand.js"] = (Path.Combine(tableDirectory, "hand.js"), JavaScript),
            ["/guidance.js"] = (Path.Combine(tableDirectory, "guidance.js"), JavaScript),
            ["/practice.js"] = (Path.Combine(tableDirectory, "practice.js"), JavaScript),
            ["/startup.js"] = (Path.Combine(tableDirectory, "startup.js"), JavaScript),
            ["/feed.js"] = (Path.Combine(tableDirectory, "feed.js"), JavaScript),
            ["/replay.js"] = (Path.Combine(tableDirectory, "replay.js"), JavaScript),
            ["/notes.js"] = (Path.Combine(tableDirectory, "notes.js"), JavaScript),
            ["/waiting.js"] = (Path.Combine(tableDirectory, "waiting.js"), JavaScript),
            ["/slot.js"] = (Path.Combine(tableDirectory, "slot.js"), JavaScript),
            ["/presentation.js"] = (Path.Combine(tableDirectory, "presentation.js"), JavaScript),
            ["/terms.js"] = (Path.Combine(tableDirectory, "terms.js"), JavaScript),
            ["/combat-fx.js"] = (Path.Combine(tableDirectory, "combat-fx.js"), JavaScript),
            ["/feedback.js"] = (Path.Combine(tableDirectory, "feedback.js"), JavaScript),

            // An isolated design study with authored sample frames (ADR 0123); it never opens a seat or posts a
            // decision. Its renderer is a bundle built from arena/ into arena-build/, which is not committed: the
            // route is here, the file is there once `npm --prefix arena run build` has run. The bundle's legal
            // notices ship beside it, as esbuild links them.
            ["/arena-lab"] = (Path.Combine(tableDirectory, "arena-lab.html"), StudioResponse.Html),
            ["/arena-lab.css"] = (Path.Combine(tableDirectory, "arena-lab.css"), Css),
            ["/arena-build/game.js"] = (Path.Combine(tableDirectory, "arena-build", "game.js"), JavaScript),
            ["/arena-build/game.js.LEGAL.txt"] = (Path.Combine(tableDirectory, "arena-build", "game.js.LEGAL.txt"), StudioResponse.Plain),
            ["/arena-build/PHASER-LICENSE.txt"] = (Path.Combine(tableDirectory, "arena-build", "PHASER-LICENSE.txt"), StudioResponse.Plain),
            ["/arena-art/eclipse-arena.webp"] = (Path.Combine(tableDirectory, "arena-art", "eclipse-arena.webp"), Webp),
            ["/arena-art/creatures.webp"] = (Path.Combine(tableDirectory, "arena-art", "creatures.webp"), Webp),
            ["/arena-art/base-creature.webp"] = (Path.Combine(tableDirectory, "arena-art", "base-creature.webp"), Webp),
            ["/arena-art/brute.webp"] = (Path.Combine(tableDirectory, "arena-art", "brute.webp"), Webp),
            ["/arena-art/predator.webp"] = (Path.Combine(tableDirectory, "arena-art", "predator.webp"), Webp),
            ["/arena-art/warped.webp"] = (Path.Combine(tableDirectory, "arena-art", "warped.webp"), Webp),
            ["/arena-lab-state.js"] = (Path.Combine(tableDirectory, "arena-lab-state.js"), JavaScript),
            ["/arena-lab-fixture.js"] = (Path.Combine(tableDirectory, "arena-lab-fixture.js"), JavaScript),

            // The opt-in live renderer completes the game loop for one seat (ADRs 0125–0128). Its modules bundle into
            // live.js and the page loads none of them by name; they are served because every module beside the page
            // is (TableFilesTests), and the laboratory's browser test imports the layout to check its hit targets.
            ["/arena"] = (Path.Combine(tableDirectory, "arena.html"), StudioResponse.Html),
            ["/arena.css"] = (Path.Combine(tableDirectory, "arena.css"), Css),
            ["/arena-combat.js"] = (Path.Combine(tableDirectory, "arena-combat.js"), JavaScript),
            ["/arena-mutations.js"] = (Path.Combine(tableDirectory, "arena-mutations.js"), JavaScript),
            ["/arena-planning.js"] = (Path.Combine(tableDirectory, "arena-planning.js"), JavaScript),
            ["/arena-match.js"] = (Path.Combine(tableDirectory, "arena-match.js"), JavaScript),
            ["/arena-guidance.js"] = (Path.Combine(tableDirectory, "arena-guidance.js"), JavaScript),
            ["/arena-upkeep.js"] = (Path.Combine(tableDirectory, "arena-upkeep.js"), JavaScript),
            ["/arena-history.js"] = (Path.Combine(tableDirectory, "arena-history.js"), JavaScript),
            ["/arena-playback.js"] = (Path.Combine(tableDirectory, "arena-playback.js"), JavaScript),
            ["/arena-live.js"] = (Path.Combine(tableDirectory, "arena-live.js"), JavaScript),
            ["/arena-seats.js"] = (Path.Combine(tableDirectory, "arena-seats.js"), JavaScript),
            ["/arena-art.js"] = (Path.Combine(tableDirectory, "arena-art.js"), JavaScript),
            ["/arena-layout.js"] = (Path.Combine(tableDirectory, "arena-layout.js"), JavaScript),
            ["/arena-timing.js"] = (Path.Combine(tableDirectory, "arena-timing.js"), JavaScript),
            ["/arena-build/live.js"] = (Path.Combine(tableDirectory, "arena-build", "live.js"), JavaScript),
            ["/arena-build/live.js.LEGAL.txt"] = (Path.Combine(tableDirectory, "arena-build", "live.js.LEGAL.txt"), StudioResponse.Plain),

            // The pilot's own page. It is a route of this host rather than a second one, because it reads the
            // same session -- and it is reached only by somebody holding the pilot token, which the console
            // prints and no player's link carries.
            ["/pilot"] = (Path.Combine(tableDirectory, "pilot.html"), StudioResponse.Html),
            ["/pilot.css"] = (Path.Combine(tableDirectory, "pilot.css"), Css),
            ["/pilot.js"] = (Path.Combine(tableDirectory, "pilot.js"), JavaScript),

            // The operator's admin panel: the tables this host is playing, the form that opens one, and the
            // sessions its store holds (ADR 0081). A route of this host for the same reason the pilot is, and
            // fenced the same way: by what the request carries, not by the page being secret.
            ["/admin"] = (Path.Combine(tableDirectory, "admin.html"), StudioResponse.Html),
            ["/admin.css"] = (Path.Combine(tableDirectory, "admin.css"), Css),
            ["/admin.js"] = (Path.Combine(tableDirectory, "admin.js"), JavaScript),
        };
    }

    /// <summary>The routes this host serves, so a test can hold them against what the page actually imports.</summary>
    public IReadOnlyCollection<string> Routes => _routes.Keys;

    public StudioResponse Get(string path)
    {
        if (path == "/favicon.ico")
        {
            return new StudioResponse(204, "image/x-icon", []);
        }

        if (!_routes.TryGetValue(path, out var route))
        {
            return StudioResponse.OfPlainText(404, $"No such page: {path}");
        }

        if (!File.Exists(route.Path))
        {
            return StudioResponse.OfPlainText(500, $"'{route.Path}' is missing. Run the table from the repository root.");
        }

        // A bitmap is bytes; everything else the table ships is text.
        return route.ContentType is Png or Webp
            ? new StudioResponse(200, route.ContentType, File.ReadAllBytes(route.Path))
            : StudioResponse.OfText(200, route.ContentType, File.ReadAllText(route.Path));
    }
}
