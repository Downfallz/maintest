using System.Net;
using DownfallArena.Cli.Hosting;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The table's HTTP host: the page, and one seat's API. It binds to the loopback address, which is enough for
/// two people around one screen and is where stage 1 stops.
/// </summary>
/// <remarks>
/// Two fences, and they answer different attacks. The same-origin check refuses a request another page made
/// on the player's behalf, exactly as the studio's does, which is why it is the same one (ADR 0015,
/// <see cref="LoopbackHost.CrossSite" />). The seat token refuses a request that asks for a seat it does not
/// hold — the one that matters here, because hotseat puts both tokens in one browser and a leak would produce
/// a playtest that looks perfectly normal and is worthless.
/// </remarks>
internal sealed class TableServer : IDisposable
{
    private readonly LoopbackHost _host;
    private readonly TableApi _api;
    private readonly TableFiles _files;

    public TableServer(int port, TableApi api, TableFiles files)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(files);

        _api = api;
        _files = files;
        _host = new LoopbackHost(port, AnswerAsync);
    }

    public string Url => _host.Url;

    public Task RunAsync(CancellationToken cancellationToken) => _host.RunAsync(cancellationToken);

    public void Dispose() => _host.Dispose();

    private async Task<StudioResponse> AnswerAsync(HttpListenerRequest request, string body)
    {
        var path = request.Url?.AbsolutePath ?? "/";
        if (!path.StartsWith("/api/", StringComparison.Ordinal))
        {
            return _files.Get(path);
        }

        var method = request.HttpMethod;
        return LoopbackHost.CrossSite(request.Headers["Sec-Fetch-Site"], method, request.ContentType, "table") is { } refusal
            ? refusal
            : await _api.HandleAsync(method, path, body, request.Headers[TableApi.TokenHeader]);
    }
}
