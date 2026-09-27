using System.Net;
using DownfallArena.Cli.Hosting;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The table's HTTP host: the pages, the lobby, every table's API, and the short code a player types to reach
/// their seat. It binds the address it is given — the loopback one by default, the machine's own when the
/// players are two people passing a phone across a table (ADR 0054), and every interface in its container
/// (ADR 0080).
/// </summary>
/// <remarks>
/// <para>
/// Two fences, and they answer different attacks. The same-origin check refuses a request another page made
/// on the player's behalf, exactly as the studio's does, which is why it is the same one (ADR 0015,
/// <see cref="HttpHost.CrossSite" />). The seat token refuses a request that asks for a seat it does not
/// hold — the one that matters here, because hotseat puts both tokens in one browser and a leak would produce
/// a playtest that looks perfectly normal and is worthless. It is also the fence that holds when the host
/// leaves the loopback address, where the other one no longer says anything about who is on the network.
/// </para>
/// <para>
/// Which table a request is for is what its token says (ADR 0081): the registry finds the table a seat or
/// pilot token belongs to, and that table's API answers as it always did. The lobby is the one set of routes
/// that reaches no table, and it is behind the operator's door instead.
/// </para>
/// </remarks>
internal sealed class TableServer : IDisposable
{
    private const string SessionPrefix = "/session/";

    private readonly HttpHost _host;
    private readonly TableRegistry _registry;
    private readonly TableFiles _files;
    private readonly LobbyApi _lobby;
    private readonly TimeProvider _clock;

    public TableServer(string address, int port, TableRegistry registry, TableFiles files, LobbyApi lobby, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(lobby);
        ArgumentNullException.ThrowIfNull(clock);

        _registry = registry;
        _files = files;
        _lobby = lobby;
        _clock = clock;
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
    private async Task<StudioResponse> SessionPageAsync(string id)
    {
        if (_registry.ById(id) is not { } table)
        {
            return StudioResponse.OfPlainText(404, $"No session '{id}' at this host.");
        }

        if (table.Run is not { } run)
        {
            return StudioResponse.OfPlainText(404, "This table is not recording a session.");
        }

        if (!table.Api.IsDecided)
        {
            return StudioResponse.OfPlainText(409, "The session is still being played. Its trace carries both seats' boards, so it is served once the match has an outcome.");
        }

        // Decided is not the same as written down. The host closes the dataset after the outcome, and a page
        // served in that window would show a run whose manifest still says it played nothing.
        if (!run.IsClosed)
        {
            return StudioResponse.OfPlainText(409, "The session has just ended and is still being written. Ask again in a moment.");
        }

        try
        {
            var artifacts = await run.ArtifactsAsync();
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
            return _registry.Codes.Answer(path);
        }

        if (!path.StartsWith("/api/", StringComparison.Ordinal) && !path.StartsWith(SessionPrefix, StringComparison.Ordinal))
        {
            return _files.Get(path);
        }

        // Both the API and the session page go behind the same-origin fence. The session page especially: it
        // carries the trace, so a page a player has open elsewhere must not be able to frame it and read the
        // half of the match its seat never saw. A top-level navigation sends `none` and is allowed; a frame on
        // another site sends `cross-site` and is not.
        var method = request.HttpMethod;
        if (HttpHost.CrossSite(request.Headers["Sec-Fetch-Site"], method, request.ContentType, "table") is { } refusal)
        {
            return refusal;
        }

        if (path.StartsWith(SessionPrefix, StringComparison.Ordinal))
        {
            return await SessionPageAsync(path[SessionPrefix.Length..].TrimEnd('/'));
        }

        // A host whose lobby nobody opens still lets go of the tables nobody is at: any request may sweep,
        // once a minute at most.
        var now = _clock.GetUtcNow();
        foreach (var gone in _registry.SweepIfDue(now))
        {
            Console.WriteLine($"  Session {gone} let go of: nobody has asked for it in a while.");
        }

        var token = request.Headers[TableApi.TokenHeader];
        if (LobbyApi.Names(path))
        {
            return await _lobby.HandleAsync(method, path, body, token, request.Headers[OperatorGate.PrincipalHeader]);
        }

        // The token says which table, and the table's own API says the rest. A token no table holds is
        // answered as a missing one: which half of it was wrong is not information a stranger is given.
        if (_registry.ByToken(token) is not { } table)
        {
            return StudioResponse.OfPlainText(403, $"Every request carries the seat's own '{TableApi.TokenHeader}'.");
        }

        table.Touch(now);
        return await table.Api.HandleAsync(method, path, body, token, request.Headers["If-None-Match"], request.Url?.Query);
    }
}
