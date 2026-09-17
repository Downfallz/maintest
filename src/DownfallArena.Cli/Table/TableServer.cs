using System.Net;
using DownfallArena.Cli.Hosting;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The table's HTTP host: the page, one seat's API, and the short code a player types to reach their seat. It
/// binds the address it is given — the loopback one by default, and the machine's own when the players are two
/// people passing a phone across a table (ADR 0054).
/// </summary>
/// <remarks>
/// Two fences, and they answer different attacks. The same-origin check refuses a request another page made
/// on the player's behalf, exactly as the studio's does, which is why it is the same one (ADR 0015,
/// <see cref="HttpHost.CrossSite" />). The seat token refuses a request that asks for a seat it does not
/// hold — the one that matters here, because hotseat puts both tokens in one browser and a leak would produce
/// a playtest that looks perfectly normal and is worthless. It is also the fence that holds when the host
/// leaves the loopback address, where the other one no longer says anything about who is on the network.
/// </remarks>
internal sealed class TableServer : IDisposable
{
    private const string SessionPrefix = "/session/";

    private readonly HttpHost _host;
    private readonly TableApi _api;
    private readonly TableFiles _files;
    private readonly JoinCodes _codes;
    private readonly PlaytestRun? _run;

    public TableServer(string address, int port, TableApi api, TableFiles files, JoinCodes codes, PlaytestRun? run = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(codes);

        _api = api;
        _files = files;
        _codes = codes;
        _run = run;
        _host = new HttpHost(address, port, AnswerAsync);
    }

    public string Url => _host.Url;

    /// <summary>Whether only this machine can reach the table, which is a thing its players are told.</summary>
    public bool IsLoopback => _host.IsLoopback;

    public Task RunAsync(CancellationToken cancellationToken) => _host.RunAsync(cancellationToken);

    public void Dispose() => _host.Dispose();

    /// <summary>
    /// The finished session in the viewer, the way the studio serves a run (ADR 0015). It carries the trace,
    /// which holds both seats' boards on purpose — so it is served only once the match has an outcome. During
    /// a session it is a refusal and not a filtered page: a page that shows one seat what the other one chose
    /// produces a playtest that looks perfectly normal and is worthless (ADR 0054).
    /// </summary>
    /// <remarks>
    /// It carries no seat token. The page is the whole session and belongs to both players, the host binds one
    /// address they are both at, and there is nothing left to hide once the match is decided.
    /// </remarks>
    private StudioResponse SessionPage(string id)
    {
        if (_run is not { } run)
        {
            return StudioResponse.OfPlainText(404, "This table is not recording a session.");
        }

        if (!string.Equals(id, run.SessionId, StringComparison.Ordinal))
        {
            return StudioResponse.OfPlainText(404, $"This host is playing session {run.SessionId}, not '{id}'. One session per process (ADR 0054).");
        }

        if (!_api.IsOver)
        {
            return StudioResponse.OfPlainText(409, "The session is still being played. Its trace carries both seats' boards, so it is served once the match has an outcome.");
        }

        try
        {
            var artifacts = run.Artifacts();
            return artifacts.Count == 0
                ? StudioResponse.OfPlainText(404, $"Session '{id}' has no artifact to show.")
                : StudioResponse.OfText(200, StudioResponse.Html, ViewerPage.Render(StudioHost.ViewerDirectory, id, artifacts));
        }
        catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or FileNotFoundException or InvalidDataException)
        {
            return StudioResponse.OfPlainText(404, exception.Message);
        }
    }

    private async Task<StudioResponse> AnswerAsync(HttpListenerRequest request, string body)
    {
        var path = request.Url?.AbsolutePath ?? "/";
        if (JoinCodes.Names(path))
        {
            return _codes.Answer(path);
        }

        if (path.StartsWith(SessionPrefix, StringComparison.Ordinal))
        {
            return SessionPage(path[SessionPrefix.Length..].TrimEnd('/'));
        }

        if (!path.StartsWith("/api/", StringComparison.Ordinal))
        {
            return _files.Get(path);
        }

        var method = request.HttpMethod;
        return HttpHost.CrossSite(request.Headers["Sec-Fetch-Site"], method, request.ContentType, "table") is { } refusal
            ? refusal
            : await _api.HandleAsync(method, path, body, request.Headers[TableApi.TokenHeader], request.Headers["If-None-Match"], request.Url?.Query);
    }
}
