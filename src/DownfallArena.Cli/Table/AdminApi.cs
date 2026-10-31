using System.Text.Json;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The operator's routes (ADR 0081, amended): the tables this host is playing and opening a new one, the
/// sessions its store holds with their export and deletion, the bots a seat may be offered, and the one
/// question a page may ask without being the operator — whether the request it carries is theirs. Everything
/// but that last one is behind <see cref="OperatorGate" />: the players never come here, and a request that is
/// nobody's is answered with where to sign in.
/// </summary>
/// <remarks>
/// What a table answers with is what its console line used to say: each seat's code and link, the pilot's
/// link, and where the session is being written. The tokens are in it because the operator is the one person
/// they are for -- the console printed them before, and the panel is the console of a host that has none.
/// </remarks>
internal sealed class AdminApi
{
    public const string Prefix = "/api/tables";

    public const string SessionsPrefix = "/api/sessions";

    /// <summary>Whether the request is the operator's. Open to anybody, and says nothing but yes or no.</summary>
    public const string MePath = "/api/me";

    /// <summary>The operator's page, which a refusal and a redirect both name.</summary>
    public const string Page = "/admin";

    private const string Zip = "application/zip";

    private readonly TableRegistry _registry;
    private readonly TableComposer _composer;
    private readonly OperatorGate _gate;
    private readonly TimeProvider _clock;
    private readonly string _rules;
    private readonly StoredSessions? _stored;
    private readonly IReadOnlyList<SeatableAgent> _agents;

    public LobbyApi Lobby { get; }

    /// <param name="rules">The line the console prints of the rule set: which one, and where it came from.</param>
    /// <param name="stored">The host's store, or none for a host that records nothing.</param>
    /// <param name="agents">The bots a seat is offered, in the order they are offered.</param>
    public AdminApi(TableRegistry registry, TableComposer composer, OperatorGate gate, TimeProvider clock, string rules, StoredSessions? stored = null, IReadOnlyList<SeatableAgent>? agents = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(composer);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(rules);

        _registry = registry;
        _composer = composer;
        _gate = gate;
        _clock = clock;
        _rules = rules;
        _stored = stored;
        _agents = agents ?? [];
        Lobby = new LobbyApi(registry, composer, clock);
    }

    public static bool Names(string path) =>
        path == MePath || Under(path, Prefix) || Under(path, SessionsPrefix) || LobbyApi.Names(path);

    private static bool Under(string path, string prefix) => path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string? token, string? principal)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (LobbyApi.Names(path))
        {
            return await Lobby.HandleAsync(method, path, body, token);
        }

        var operatorName = _gate.Admit(token, principal);
        if (path == MePath)
        {
            // Not a refusal when it is nobody's: the table page asks this to know whether to show the way back
            // to the panel, and a player's page must read "no" rather than an error.
            return StudioResponse.OfJson(new { @operator = operatorName is not null, admin = Page }, ArtifactJson.LineOptions);
        }

        if (operatorName is null)
        {
            return Refused();
        }

        // The one moment memory is about to be spent, so the one moment tables nobody is at are let go of.
        foreach (var gone in _registry.Sweep(_clock.GetUtcNow()))
        {
            Console.WriteLine($"  Session {gone} let go of: nobody has asked for it in a while.");
        }

        if (Under(path, SessionsPrefix))
        {
            return method == "DELETE"
                ? await LobbyApi.ExclusivelyAsync(() => SessionsAsync(method, path[SessionsPrefix.Length..].Trim('/'), body, operatorName))
                : await SessionsAsync(method, path[SessionsPrefix.Length..].Trim('/'), body, operatorName);
        }

        var rest = path[Prefix.Length..].Trim('/');
        return (method, rest) switch
        {
            ("GET", "") => await ListAsync(),
            ("POST", "") => await OpenAsync(body, operatorName),
            ("DELETE", { Length: > 0 } id) => await LobbyApi.ExclusivelyAsync(() => Task.FromResult(Close(id, operatorName))),
            ("POST", _) when Reinvited(rest) is { } seat => await LobbyApi.ExclusivelyAsync(() => ReinviteAsync(seat.Table, seat.Slot, operatorName)),
            _ => StudioResponse.OfPlainText(404, $"No such route: {method} {path}"),
        };
    }

    /// <summary>
    /// Nobody's request. In front of the platform it says where to sign in, so the page can send the operator
    /// there and back; at a console it says what the console printed.
    /// </summary>
    private StudioResponse Refused() =>
        _gate.TrustsPlatform
            ? StudioResponse.OfJson(new { error = "Admin.SignIn", message = "Sign in to manage the tables.", login = OperatorGate.Login }, ArtifactJson.LineOptions, status: 401)
            : StudioResponse.OfJson(new { error = "Admin.Token", message = $"The panel carries the operator's token the console printed, as '{TableApi.TokenHeader}'." }, ArtifactJson.LineOptions, status: 403);

    private async Task<StudioResponse> ListAsync() =>
        StudioResponse.OfJson(
            new
            {
                rules = _rules,

                // The formats a chooser offers and the one it opens on, answered here for the same reason the
                // bots are: a page that listed them itself would be a page holding a rule about the game
                // (ADR 0054). The host's own is in the list whether or not it is one of the three offered.
                formats = Formats().Select(format => new { value = format.ToString(), teamSize = format.TeamSize, picks = _composer.Rules.InFormat(format).EvolutionPicksUsableInAnOpportunity }).ToArray(),
                format = _composer.Rules.Format.ToString(),

                // When a spell is chosen (ADR 0110), the host's and the ones a table may be opened with.
                declarations = new[] { CombatDeclarationText.HiddenIntent, CombatDeclarationText.AtActivation },
                declaration = CombatDeclarationText.Of(_composer.Rules.CombatDeclaration),
                recording = _composer.Records,
                agents = _agents.Select(agent => new { value = agent.Value, label = agent.Label, featured = agent.Featured }).ToArray(),
                tables = await Task.WhenAll(_registry.All().Select(DescribedAsync)),
            },
            ArtifactJson.LineOptions);

    /// <summary>The formats a chooser is offered: the shortlist, and this host's own when it is not on it.</summary>
    private IEnumerable<MatchFormat> Formats() =>
        MatchFormat.Offered.Contains(_composer.Rules.Format)
            ? MatchFormat.Offered
            : [.. MatchFormat.Offered, _composer.Rules.Format];

    private async Task<StudioResponse> OpenAsync(string body, string operatorName)
    {
        var request = TableRequest.Parse(body, out var problem);
        if (problem is not null)
        {
            return StudioResponse.OfPlainText(400, problem);
        }

        // The place is taken before composing, because composing opens the recording and starts the match: a
        // table refused afterwards would have written a run nobody played.
        using var reservation = _registry.Reserve();
        if (reservation is null)
        {
            return Full();
        }

        PlayedTable table;
        try
        {
            table = await _composer.ComposeAsync(request);
        }
        catch (Exception failure) when (failure is ArgumentException or IOException or JsonException or InvalidDataException)
        {
            // A weights file that is not there, a policy trained under another schema: the operator naming
            // the wrong thing, answered as such rather than as a broken host.
            return StudioResponse.OfPlainText(400, failure.Message);
        }

        _registry.Add(table, reservation);
        await table.RecordAsync(_registry.Codes);
        Console.WriteLine($"  Session {table.Id} opened by {operatorName}: {string.Join(", ", table.Seats.Select(seat => $"{seat.Name} {(seat.Person is null ? table.Session.Seat(seat.Slot).Seated.Name : $"code {_registry.Codes.Of(seat)}")}"))}");
        return StudioResponse.OfJson(await DescribedAsync(table), ArtifactJson.LineOptions, status: 201);
    }

    private StudioResponse Full() =>
        StudioResponse.OfJson(
            new { error = "Admin.Full", message = $"This host has {_registry.Capacity} tables open, waiting or under way, which is as many as it takes. Close one first." },
            ArtifactJson.LineOptions,
            status: 409);

    /// <summary>The table and seat of <c>&lt;id&gt;/seats/&lt;seat&gt;/invite</c>, or null for any other path.</summary>
    private static (string Table, string Slot)? Reinvited(string rest)
    {
        var parts = rest.Split('/');
        return parts is [{ Length: > 0 } table, "seats", { Length: > 0 } slot, "invite"] ? (table, slot) : null;
    }

    /// <summary>
    /// A new invitation for a person's seat, for a player who lost the device holding it (ADR 0116): the seat's
    /// token is replaced, so that device stops reaching it, and a fresh invitation and code are what the operator
    /// passes on. The match and the question the seat is asked stay as they are.
    /// </summary>
    private async Task<StudioResponse> ReinviteAsync(string id, string slot, string operatorName)
    {
        if (_registry.ById(id) is not { IsOver: false } table)
        {
            return StudioResponse.OfPlainText(404, $"No unfinished table '{id}' at this host.");
        }

        if (table.Seats.FirstOrDefault(seat => seat.Name == slot && seat.Person is not null) is not { } seat)
        {
            return StudioResponse.OfPlainText(404, $"No person's seat '{slot}' at table '{id}'.");
        }

        var previous = _registry.KeysOf(seat);
        var arrived = seat.Person!.HasArrived;
        if (!_registry.Rekey(table, seat, SeatKeys.Offered()))
        {
            return StudioResponse.OfPlainText(404, $"No unfinished table '{id}' at this host.");
        }

        seat.Person.Depart();
        try
        {
            await table.RecordAsync(_registry.Codes);
        }
        catch (Exception failure)
        {
            // Whatever failed, memory goes back to what the record still says: a seat rekeyed here and not on
            // the store would let the cut-off device back in after a redeploy, and kill the invitation passed on.
            _registry.Rekey(table, seat, previous);
            if (arrived)
            {
                seat.Person.Arrive();
            }

            if (failure is IOException or UnauthorizedAccessException or Azure.RequestFailedException)
            {
                return StudioResponse.OfPlainText(503, $"The new invitation could not be saved, so the seat keeps its old one: {failure.Message}");
            }

            throw;
        }

        Console.WriteLine($"  Session {id}: {slot} invited again by {operatorName}.");
        var code = _registry.Codes.Of(seat);
        return StudioResponse.OfJson(new { slot, code, join = code is null ? null : $"{JoinCodes.Prefix}{code}", link = $"/?{seat.Name}={seat.Key}" }, ArtifactJson.LineOptions);
    }

    private StudioResponse Close(string id, string operatorName)
    {
        if (!_registry.Remove(id))
        {
            return StudioResponse.OfPlainText(404, $"No table '{id}' at this host.");
        }

        Console.WriteLine($"  Session {id} closed by {operatorName}.");
        return new StudioResponse(204, StudioResponse.Plain, []);
    }

    /// <summary>
    /// The store's side: <c>GET</c> lists every run, <c>DELETE</c> with <c>{"ids":[...]}</c> removes the runs
    /// named (closing their tables first when they are still being played), <c>GET /&lt;id&gt;/export</c> is
    /// one run as a zip and <c>POST /export</c> with <c>{"ids":[...]}</c> is several, a directory each.
    /// </summary>
    private async Task<StudioResponse> SessionsAsync(string method, string rest, string body, string operatorName)
    {
        if (_stored is not { } stored)
        {
            return StudioResponse.OfJson(new { error = "Admin.NotRecording", message = "This host records nothing (--no-record), so there is no session to list, export or delete." }, ArtifactJson.LineOptions, status: 404);
        }

        switch (method, rest)
        {
            case ("GET", ""):
                return StudioResponse.OfJson(new { sessions = (await stored.ListAsync()).Select(Described).ToArray() }, ArtifactJson.LineOptions);
            case ("DELETE", ""):
                return await DeleteAsync(stored, Ids(body, out var problem), problem, operatorName);
            case ("POST", "export"):
                return await ExportAsync(stored, Ids(body, out var asked), asked, "sessions");
            default:
                if (method == "GET" && rest.EndsWith("/export", StringComparison.Ordinal))
                {
                    var id = rest[..^"/export".Length];
                    return await ExportAsync(stored, [id], problem: null, id);
                }

                if (method == "GET" && rest.EndsWith("/replay", StringComparison.Ordinal))
                {
                    return await ReplayAsync(stored, rest[..^"/replay".Length]);
                }

                return StudioResponse.OfPlainText(404, $"No such route: {method} {SessionsPrefix}/{rest}");
        }
    }

    /// <summary>The events a finished match's replay reads: every one public, so nothing either seat held is served.</summary>
    private static readonly HashSet<string> Replayed = ["OngoingEffectsApplied", "PurchasesRevealed", "CombatActionResolved", "RoundEnded"];

    /// <summary>
    /// A finished session as the table page replays it: the match's outcome, each seat's last board, the catalogue
    /// its table served and the public events of every round, off the run the store holds. A table this host has
    /// let go of, or that an earlier replica played, is replayed as well as one still listed; a match still being
    /// played is not, since its trace is a checkpoint and not the match.
    /// </summary>
    private static async Task<StudioResponse> ReplayAsync(StoredSessions stored, string id)
    {
        var (trace, catalogue) = await stored.MatchAsync(id);
        if (trace is null)
        {
            return StudioResponse.OfJson(new { error = "Admin.NoMatch", message = $"Session {id} recorded no match to replay." }, ArtifactJson.LineOptions, status: 404);
        }

        using var match = JsonDocument.Parse(trace);
        var root = match.RootElement;
        var entries = root.GetProperty("entries");
        if (!root.TryGetProperty("outcome", out var outcome) || outcome.ValueKind != JsonValueKind.Object || entries.GetArrayLength() == 0)
        {
            return StudioResponse.OfJson(new { error = "Admin.NotOver", message = $"Session {id} has not finished its match, so there is nothing to replay yet." }, ArtifactJson.LineOptions, status: 409);
        }

        var last = entries[entries.GetArrayLength() - 1];
        using var served = catalogue is null ? null : JsonDocument.Parse(catalogue);
        return StudioResponse.OfJson(
            new
            {
                id,
                // As the seat route serves it: the winner by its seat's name, which the page compares with its own.
                outcome = new
                {
                    winner = outcome.TryGetProperty("winner", out var winner) && Enum.TryParse<PlayerSlot>(winner.GetString(), out var slot) ? TableSeat.NameOf(slot) : null,
                    reason = outcome.TryGetProperty("reason", out var reason) ? reason.GetString() : null,
                },
                boards = new { player1 = last.GetProperty("player1"), player2 = last.GetProperty("player2") },
                catalogue = served?.RootElement,
                feed = entries.EnumerateArray()
                    .Where(entry => Replayed.Contains(entry.GetProperty("event").GetProperty("kind").GetString() ?? string.Empty))
                    .Select(entry => new { sequence = entry.GetProperty("sequence"), round = entry.GetProperty("round"), subPhase = entry.GetProperty("subPhase"), @event = entry.GetProperty("event") })
                    .ToArray(),
            },
            ArtifactJson.LineOptions);
    }

    private async Task<StudioResponse> DeleteAsync(StoredSessions stored, IReadOnlyList<string> ids, string? problem, string operatorName)
    {
        if (problem is not null)
        {
            return StudioResponse.OfPlainText(400, problem);
        }

        var deleted = new List<string>();
        var missing = new List<string>();
        foreach (var id in ids)
        {
            // A table still being played is closed first: its match is stopped and its codes stop answering.
            // And then waited for: a checkpoint or a note still on its way would otherwise land after the
            // deletion and bring the run back, on the blob store as a prefix and here as a directory.
            var table = _registry.ById(id);
            var closed = _registry.Remove(id);
            if (closed && table is not null)
            {
                await table.QuiesceAsync();
            }

            var removed = await stored.DeleteAsync(id);
            (closed || removed ? deleted : missing).Add(id);
        }

        if (deleted.Count > 0)
        {
            Console.WriteLine($"  Sessions deleted by {operatorName}: {string.Join(", ", deleted)}.");
        }

        return StudioResponse.OfJson(new { deleted, missing }, ArtifactJson.LineOptions);
    }

    private async Task<StudioResponse> ExportAsync(StoredSessions stored, IReadOnlyList<string> ids, string? problem, string name)
    {
        if (problem is not null)
        {
            return StudioResponse.OfPlainText(400, problem);
        }

        // A run being written is not a run to export: a checkpoint half written, or a line half appended,
        // would travel as a file. It is exportable once its match is over, or once its table is closed.
        var live = ids.Where(id => _registry.ById(id) is { IsOver: false }).ToList();
        if (live.Count > 0)
        {
            return StudioResponse.OfJson(
                new { error = "Admin.Live", message = $"Still being played, so not exported: {string.Join(", ", live)}. Wait for the match to end, or close the table first." },
                ArtifactJson.LineOptions,
                status: 409);
        }

        var (zip, exported, missing) = await stored.ZipAsync(ids);
        if (exported.Count == 0)
        {
            return StudioResponse.OfPlainText(404, $"Nothing to export: no session named {string.Join(", ", missing)} holds any file.");
        }

        return new StudioResponse(200, Zip, zip, [new KeyValuePair<string, string>("Content-Disposition", $"attachment; filename=\"{name}.zip\"")]);
    }

    /// <summary>The ids a body names, or why it names none. A body is <c>{"ids":["...", ...]}</c>.</summary>
    private static List<string> Ids(string body, out string? problem)
    {
        problem = null;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (!document.RootElement.TryGetProperty("ids", out var ids) || ids.ValueKind != JsonValueKind.Array)
            {
                problem = "The body names the sessions as {\"ids\": [\"<id>\", ...]}.";
                return [];
            }

            var named = ids.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!).Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToList();
            if (named.Count == 0)
            {
                problem = "No session is named.";
            }

            return named;
        }
        catch (JsonException)
        {
            problem = "The body is not JSON.";
            return [];
        }
    }

    /// <summary>One stored run as the panel lists it, with what its table says when this host still has it.</summary>
    private object Described(StoredSession session)
    {
        var table = _registry.ById(session.Id);
        return new
        {
            id = session.Id,
            createdAt = session.CreatedAt,
            player1 = session.Player1Agent,
            player2 = session.Player2Agent,
            seed = session.Seed,
            matches = session.Matches,
            steps = session.Steps,
            location = session.Location,
            live = table is not null,
            over = table?.IsOver ?? session.Matches > 0,
            session = $"/session/{session.Id}",
            replay = (table?.IsOver ?? session.Matches > 0) ? ReplayPath(session.Id) : null,
            export = $"{SessionsPrefix}/{session.Id}/export",
        };
    }

    /// <summary>One table as the panel shows it: where it is, who is in each seat, and how to reach it.</summary>
    private async Task<object> DescribedAsync(PlayedTable table) =>
        new
        {
            id = table.Id,
            createdAt = table.CreatedAt,
            over = table.IsOver,
            finished = table.IsFinished,

            // The format this table is playing, which is not necessarily the host's: the panel opens one per
            // table, so a row that showed the host's would name the wrong game.
            format = table.Session.Rules.Format.ToString(),
            declaration = CombatDeclarationText.Of(table.Session.Rules.CombatDeclaration),

            // Waiting for its people to reach their seats (ADR 0092): no question has been asked yet.
            waiting = !table.Session.HasBegun && !table.IsOver,

            // The round, or null while the match is busy: the page says "playing" of that, never "waiting".
            round = table.Session.IsOver ? null : await RoundNowAsync(table),
            location = table.Run?.Location,
            seats = table.Seats.Select(seat => new
            {
                slot = seat.Name,
                seated = table.Session.Seat(seat.Slot).Seated.Name,
                hasPerson = seat.Person is not null,
                code = _registry.Codes.Of(seat),
                join = _registry.Codes.Of(seat) is { } code ? $"{JoinCodes.Prefix}{code}" : null,
                link = seat.Key is { } key ? $"/?{seat.Name}={key}" : null,

                // Somebody's browser holds it (ADR 0116): no link reaches it, and a new invitation is the way
                // back in for a player who lost that browser.
                taken = seat.Person is not null && seat.Taken,
            }).ToArray(),

            // One link carrying both seats' invitations, for the case where the two people share one browser:
            // the page follows whichever seat the match asks. None once either seat is taken.
            hotseat = table.Seats.Count(seat => seat.Key is not null) > 1
                ? $"/?{string.Join('&', table.Seats.Where(seat => seat.Key is not null).Select(seat => $"{seat.Name}={seat.Key}"))}"
                : null,
            pilot = $"/pilot?token={table.Pilot.Token}",
            session = $"/session/{table.Id}",
            replay = table.IsOver ? ReplayPath(table.Id) : null,
        };

    /// <summary>The table page replaying a finished session, action by action (<see cref="ReplayAsync" />).</summary>
    private static string ReplayPath(string id) => $"/play?replay={Uri.EscapeDataString(id)}";

    /// <summary>
    /// The round a table is in, read without waiting long on it: a listing must not block behind a match that
    /// is in the middle of a command, so a table whose gate is busy reads as "somewhere" rather than holding
    /// the whole list. A read that failed -- the table was let go of underneath -- reads the same way.
    /// </summary>
    internal static async Task<int?> RoundNowAsync(PlayedTable table)
    {
        var reading = table.RoundAsync();
        var first = await Task.WhenAny(reading, Task.Delay(TimeSpan.FromMilliseconds(200)));
        return first == reading && reading.IsCompletedSuccessfully ? reading.Result : null;
    }
}
