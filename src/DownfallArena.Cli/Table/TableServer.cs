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
    private readonly HttpHost _host;
    private readonly TableApi _api;
    private readonly TableFiles _files;
    private readonly JoinCodes _codes;

    public TableServer(string address, int port, TableApi api, TableFiles files, JoinCodes codes)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(codes);

        _api = api;
        _files = files;
        _codes = codes;
        _host = new HttpHost(address, port, AnswerAsync);
    }

    public string Url => _host.Url;

    /// <summary>Whether only this machine can reach the table, which is a thing its players are told.</summary>
    public bool IsLoopback => _host.IsLoopback;

    public Task RunAsync(CancellationToken cancellationToken) => _host.RunAsync(cancellationToken);

    public void Dispose() => _host.Dispose();

    private async Task<StudioResponse> AnswerAsync(HttpListenerRequest request, string body)
    {
        var path = request.Url?.AbsolutePath ?? "/";
        if (JoinCodes.Names(path))
        {
            return _codes.Answer(path);
        }

        if (!path.StartsWith("/api/", StringComparison.Ordinal))
        {
            return _files.Get(path);
        }

        var method = request.HttpMethod;
        return HttpHost.CrossSite(request.Headers["Sec-Fetch-Site"], method, request.ContentType, "table") is { } refusal
            ? refusal
            : await _api.HandleAsync(method, path, body, request.Headers[TableApi.TokenHeader]);
    }
}
