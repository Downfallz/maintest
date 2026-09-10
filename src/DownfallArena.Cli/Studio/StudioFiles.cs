namespace DownfallArena.Cli.Studio;

/// <summary>
/// The static files the studio serves, as a fixed route table. Nothing is derived from the request path, so the
/// host cannot be talked into reading a file the studio does not ship.
/// </summary>
internal sealed class StudioFiles
{
    private readonly Dictionary<string, (string Path, string ContentType)> _routes;

    public StudioFiles(string studioDirectory, string viewerDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studioDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerDirectory);

        var index = (Path.Combine(studioDirectory, "index.html"), StudioResponse.Html);
        _routes = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["/"] = index,
            ["/index.html"] = index,
            ["/studio.css"] = (Path.Combine(studioDirectory, "studio.css"), "text/css; charset=utf-8"),
            ["/studio.js"] = (Path.Combine(studioDirectory, "studio.js"), "text/javascript; charset=utf-8"),
            ["/backend.js"] = (Path.Combine(studioDirectory, "backend.js"), "text/javascript; charset=utf-8"),
            ["/viewer.css"] = (Path.Combine(viewerDirectory, "viewer.css"), "text/css; charset=utf-8"),
        };
    }

    public StudioResponse Get(string path)
    {
        // The browser asks for this on its own and the studio ships no icon; 204 answers it without a console error.
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
            : StudioResponse.OfPlainText(500, $"'{route.Path}' is missing. Run the studio from the repository root.");
    }
}
