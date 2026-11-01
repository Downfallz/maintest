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
            ["/lobby.css"] = (Path.Combine(tableDirectory, "lobby.css"), Css),
            ["/table.css"] = (Path.Combine(tableDirectory, "table.css"), Css),
            ["/table.js"] = (Path.Combine(tableDirectory, "table.js"), JavaScript),
            ["/transport.js"] = (Path.Combine(tableDirectory, "transport.js"), JavaScript),
            ["/seats.js"] = (Path.Combine(tableDirectory, "seats.js"), JavaScript),
            ["/session.js"] = (Path.Combine(tableDirectory, "session.js"), JavaScript),
            ["/invitation.js"] = (Path.Combine(tableDirectory, "invitation.js"), JavaScript),
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
            ["/terms.js"] = (Path.Combine(tableDirectory, "terms.js"), JavaScript),

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
        return route.ContentType == Png
            ? new StudioResponse(200, Png, File.ReadAllBytes(route.Path))
            : StudioResponse.OfText(200, route.ContentType, File.ReadAllText(route.Path));
    }
}
