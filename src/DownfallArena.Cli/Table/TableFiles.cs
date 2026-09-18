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

    private readonly Dictionary<string, (string Path, string ContentType)> _routes;

    public TableFiles(string tableDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableDirectory);

        var index = (Path.Combine(tableDirectory, "index.html"), StudioResponse.Html);
        _routes = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["/"] = index,
            ["/index.html"] = index,
            ["/table.css"] = (Path.Combine(tableDirectory, "table.css"), Css),
            ["/table.js"] = (Path.Combine(tableDirectory, "table.js"), JavaScript),
            ["/transport.js"] = (Path.Combine(tableDirectory, "transport.js"), JavaScript),
            ["/seats.js"] = (Path.Combine(tableDirectory, "seats.js"), JavaScript),
            ["/session.js"] = (Path.Combine(tableDirectory, "session.js"), JavaScript),
            ["/card.js"] = (Path.Combine(tableDirectory, "card.js"), JavaScript),
            ["/board.js"] = (Path.Combine(tableDirectory, "board.js"), JavaScript),
            ["/timeline.js"] = (Path.Combine(tableDirectory, "timeline.js"), JavaScript),
            ["/mat.js"] = (Path.Combine(tableDirectory, "mat.js"), JavaScript),
            ["/hand.js"] = (Path.Combine(tableDirectory, "hand.js"), JavaScript),
            ["/feed.js"] = (Path.Combine(tableDirectory, "feed.js"), JavaScript),
            ["/notes.js"] = (Path.Combine(tableDirectory, "notes.js"), JavaScript),
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

        return File.Exists(route.Path)
            ? StudioResponse.OfText(200, route.ContentType, File.ReadAllText(route.Path))
            : StudioResponse.OfPlainText(500, $"'{route.Path}' is missing. Run the table from the repository root.");
    }
}
