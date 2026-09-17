using System.Net;
using DownfallArena.Cli.Hosting;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// The studio's HTTP host: the static page, the JSON API, and the viewer page of a run. It binds to the loopback
/// address only — this is an authoring tool for the machine it runs on, not a service (ADR 0015).
/// </summary>
internal sealed class StudioServer : IDisposable
{
    private const string RunPrefix = "/runs/";

    private const string ComparePrefix = "/compare/";

    private readonly LoopbackHost _host;
    private readonly StudioApi _api;
    private readonly StudioFiles _files;
    private readonly string _viewerDirectory;

    public StudioServer(int port, StudioApi api, StudioFiles files, string viewerDirectory)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerDirectory);

        _api = api;
        _files = files;
        _viewerDirectory = viewerDirectory;
        _host = new LoopbackHost(port, AnswerAsync);
    }

    public string Url => _host.Url;

    public Task RunAsync(CancellationToken cancellationToken) => _host.RunAsync(cancellationToken);

    public void Dispose() => _host.Dispose();

    private async Task<StudioResponse> AnswerAsync(HttpListenerRequest request, string body)
    {
        var path = request.Url?.AbsolutePath ?? "/";
        var method = request.HttpMethod;

        if (path.StartsWith("/api/", StringComparison.Ordinal))
        {
            return LoopbackHost.CrossSite(request.Headers["Sec-Fetch-Site"], method, request.ContentType, "studio") is { } refusal
                ? refusal
                : await _api.HandleAsync(method, path, body);
        }

        if (path.StartsWith(ComparePrefix, StringComparison.Ordinal))
        {
            return Comparison(path) is var (first, second)
                ? _api.ComparePage(first, second, _viewerDirectory)
                : StudioResponse.OfPlainText(404, "A comparison is '/compare/<run>/<run>'.");
        }

        if (path.StartsWith(RunPrefix, StringComparison.Ordinal))
        {
            return _api.RunPage(path[RunPrefix.Length..].TrimEnd('/'), _viewerDirectory);
        }

        return _files.Get(path);
    }

    /// <summary>The two runs a comparison path names, or <c>null</c> when it does not name exactly two.</summary>
    public static (string First, string Second)? Comparison(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!path.StartsWith(ComparePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var runs = path[ComparePrefix.Length..].TrimEnd('/').Split('/');
        return runs.Length == 2 ? (runs[0], runs[1]) : null;
    }
}
