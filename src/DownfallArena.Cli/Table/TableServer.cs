using System.Net;
using DownfallArena.Cli.Hosting;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The table's HTTP host: the pages, the admin panel, every table's API, and the short code a player types to reach
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
/// pilot token belongs to, and that table's API answers as it always did. The admin panel is the one set of
/// routes that reaches no table, and it is behind the operator's door instead.
/// </para>
/// </remarks>
internal sealed class TableServer : IDisposable
{
    private const string SessionPrefix = "/session/";

    /// <summary>Where the operator's page was before it was the admin panel; a link printed then still arrives.</summary>
    private const string FormerLobby = "/lobby";

    private readonly HttpHost _host;
    private readonly TableRegistry _registry;
    private readonly TableFiles _files;
    private readonly AdminApi _admin;
    private readonly StoredSessions? _stored;
    private readonly TimeProvider _clock;
    private readonly AccountApi? _accounts;
    private readonly LeaderboardApi? _leaderboard;
    private readonly FriendsApi? _friends;

    /// <param name="accounts">The players' sign-in (ADR 0122), or none for a host with accounts off.</param>
    /// <param name="stored">The host's store, read for a session page whose table this process no longer has; none for a host that records nothing.</param>
    public TableServer(string address, int port, TableRegistry registry, TableFiles files, AdminApi admin, TimeProvider clock, StoredSessions? stored = null, AccountApi? accounts = null, LeaderboardApi? leaderboard = null, FriendsApi? friends = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(admin);
        ArgumentNullException.ThrowIfNull(clock);

        _registry = registry;
        _files = files;
        _admin = admin;
        _stored = stored;
        _clock = clock;
        _accounts = accounts;
        _friends = friends;
        _leaderboard = leaderboard;
        _host = new HttpHost(address, port, AnswerAsync);
    }

    public string Url => _host.Url;

    /// <summary>
    /// Whether a job tends this host's tables (ADR 0162, <c>--tended</c>): a request then lets go of the tables this
    /// process holds that nobody is at, and leaves closing the ones that never began to the job.
    /// </summary>
    public bool Tended { get; init; }

    /// <summary>
    /// Whether the host is still rebuilding the tables an earlier host left open (ADR 0091). It serves from the
    /// first moment, because a platform that waits for the port to answer gives up after a minute and a host with
    /// many records to replay takes longer (ADR 0080); but while this is true a request for a table it does not
    /// hold yet is "try again in a moment", never "no such seat", which a page would read as a seat it lost.
    /// </summary>
    public bool Starting
    {
        get => Volatile.Read(ref _starting);
        set => Volatile.Write(ref _starting, value);
    }

    private bool _starting;

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
        if (_registry.Held(id) is not { } table)
        {
            // A table nobody holds here is read off the store, unless its record says it is still open: its stored
            // trace holds both seats' boards, and it is served once the table ends. Picking it up only to refuse
            // the page would replay a match for nothing.
            if (Starting)
            {
                return StartingUp();
            }

            return await StillOpenAsync(id)
                ? StudioResponse.OfPlainText(409, "The session is still being played. Its trace carries both seats' boards, so it is served once the match has an outcome.")
                : await StoredSessionPageAsync(id);
        }

        // Somebody reading the session is somebody at the table: the hour a finished table is kept for is
        // measured from its last reader, not from its last tap.
        table.Touch(_clock.GetUtcNow());

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

    /// <summary>
    /// A session this process no longer holds, read from the store: the replica that played it is gone
    /// (ADR 0080) or the table was let go of, and the run is what survives. Nothing live is being protected
    /// here -- there is no match in memory to leak a seat of -- so what the store has is what is shown, a
    /// partial trace included.
    /// </summary>
    /// <summary>Whether a run's record says its table is open, or cannot be read to say it is not.</summary>
    private async Task<bool> StillOpenAsync(string id)
    {
        if (_stored is not { } stored || !TableKeys.IsTableId(id))
        {
            return false;
        }

        try
        {
            return await stored.RecordAsync(id) is { Status: TableRecord.Open };
        }
        catch (Exception failure) when (TableLoader.Refuses(failure) || TableRegistry.StoreFails(failure))
        {
            return true;
        }
    }

    private async Task<StudioResponse> StoredSessionPageAsync(string id)
    {
        if (_stored is not { } stored || id.Length == 0 || id.Contains('/', StringComparison.Ordinal) || id.Contains('\\', StringComparison.Ordinal) || id == "." || id == "..")
        {
            return StudioResponse.OfPlainText(404, $"No session '{id}' at this host.");
        }

        try
        {
            var artifacts = await stored.ArtifactsAsync(id);
            return artifacts.Count == 0
                ? StudioResponse.OfPlainText(404, $"No session '{id}' at this host.")
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
        if (Starting && JoinCodes.Names(path))
        {
            return StartingUp();
        }

        if (JoinCodes.Names(path))
        {
            return await _registry.AnswerAsync(path);
        }

        // Old seat links keep working; an ordinary visit opens the player lobby.
        if (path == "/" && request.Url?.Query is not { Length: > 0 })
        {
            return _files.Get("/lobby");
        }

        // Bookmarks and invitations can carry seat capabilities in their query string.
        if (path is "/arena" or "/arena-lab")
        {
            var destination = path == "/arena" ? "/battle" : "/battle-lab";
            return StudioResponse.OfRedirect(destination + request.Url?.Query);
        }

        if (path == FormerLobby)
        {
            return request.Url?.Query.Contains("token=", StringComparison.Ordinal) == true
                ? StudioResponse.OfRedirect(AdminApi.Page + request.Url.Query)
                : _files.Get("/lobby");
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

        // A host whose admin panel nobody opens still lets go of finished tables nobody reads and of tables
        // that never began and nobody came to (ADR 0115): any request may sweep, once a minute at most. On a host
        // a job tends, the job closes the tables that never began, and a request lets go of what this process
        // holds and nothing else (ADR 0162).
        var now = _clock.GetUtcNow();
        foreach (var gone in Tended ? _registry.LetGoOfIdleIfDue(now) : await _registry.SweepIfDueAsync(now))
        {
            Console.WriteLine($"  Session {gone} let go of: finished and unread, or never begun and nobody came.");
        }

        var account = new AccountRequest(request.Cookies[AccountSessions.Cookie]?.Value, Secure(request));
        if (AccountApi.Names(path))
        {
            return _accounts is null
                ? StudioResponse.OfPlainText(404, "Players do not sign in at this host (--accounts off).")
                : await _accounts.HandleAsync(method, path, body, account);
        }

        if (FriendsApi.Names(path))
        {
            return _friends is null
                ? StudioResponse.OfPlainText(404, "Players do not sign in at this host (--accounts off), so nobody has friends here.")
                : await _friends.HandleAsync(method, path, body, account);
        }

        if (LeaderboardApi.Names(path))
        {
            return _leaderboard is null
                ? StudioResponse.OfPlainText(404, "This host keeps no ladder: players do not sign in here (--accounts off).")
                : await _leaderboard.HandleAsync(method, account);
        }

        var token = request.Headers[TableApi.TokenHeader];

        // A seat's own lobby routes find their seat by its token, and a token whose table is still being rebuilt
        // would read as spent: the page would forget a seat it still has.
        if (Starting && path.StartsWith(LobbyApi.Prefix + "/", StringComparison.Ordinal))
        {
            return StartingUp();
        }

        if (AdminApi.Names(path))
        {
            return await _admin.HandleAsync(method, path, body, token, request.Headers[OperatorGate.PrincipalHeader], account);
        }

        // The token says which table, and the table's own API says the rest. A token no table holds is
        // answered as a missing one: which half of it was wrong is not information a stranger is given.
        if (await _registry.ByTokenAsync(token) is not { } table)
        {
            if (Starting && !string.IsNullOrEmpty(token))
            {
                return StartingUp();
            }

            return StudioResponse.OfPlainText(403, $"Every request carries the seat's own '{TableApi.TokenHeader}'.");
        }

        if (await SeatRefusalAsync(table, token, account) is { } refused)
        {
            return refused;
        }

        table.Touch(now);
        await LobbyApi.SeatReachedAsync(table, token);
        return await table.Api.HandleAsync(method, path, body, token, request.Headers["If-None-Match"], request.Url?.Query);
    }

    private static StudioResponse StartingUp() =>
        StudioResponse.OfPlainText(503, "This host is starting: its tables are being rebuilt. Try again in a moment.") with
        {
            Headers = [new KeyValuePair<string, string>("Retry-After", "5")],
        };

    /// <summary>
    /// On a host where players sign in, a seat is its account's (ADR 0122): its token alone is not enough. A seat
    /// that belongs to nobody yet -- one reached by a link or a code from before, or saved by a browser before its
    /// player signed in -- becomes the account's that first reaches it signed in. Null where the request may go on;
    /// a pilot's token reaches no seat and is not asked.
    /// </summary>
    private async Task<StudioResponse?> SeatRefusalAsync(PlayedTable table, string? token, AccountRequest account)
    {
        if (_accounts is null || table.Seats.FirstOrDefault(seat => seat.Token == token && seat.Person is not null) is not { } seat)
        {
            return null;
        }

        if (await _accounts.WhoAsync(account) is not { } player)
        {
            return StudioResponse.OfPlainText(401, "Sign in to play this seat: open the home page and continue with Google.");
        }

        // A finished table is only read now, and a seat nobody owned stays nobody's.
        if (seat.Account is null && (table.IsOver || await LobbyApi.BindAsync(table, seat, player.Account.Id)))
        {
            return null;
        }

        return seat.Account == player.Account.Id ? null : StudioResponse.OfPlainText(403, "This seat belongs to another player.");
    }

    /// <summary>
    /// Whether a cookie set back must be <c>Secure</c>: everywhere but a host reached at a loopback name. The
    /// container is reached over https through the platform's ingress even though the request it forwards is http,
    /// so the scheme the host sees says nothing; the name the browser used does.
    /// </summary>
    private static bool Secure(HttpListenerRequest request) =>
        request.Url is not { } url || !(url.IsLoopback || string.Equals(url.Host, "localhost", StringComparison.OrdinalIgnoreCase));
}
