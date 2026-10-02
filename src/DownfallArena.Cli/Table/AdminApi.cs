using System.Text.Json;
using DownfallArena.Cli.Studio;
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
    }

    public static bool Names(string path) =>
        path == MePath || Under(path, Prefix) || Under(path, SessionsPrefix);

    private static bool Under(string path, string prefix) => path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string? token, string? principal)
    {
        ArgumentNullException.ThrowIfNull(path);
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
            return await SessionsAsync(method, path[SessionsPrefix.Length..].Trim('/'), body, operatorName);
        }

        var rest = path[Prefix.Length..].Trim('/');
        return (method, rest) switch
        {
            ("GET", "") => await ListAsync(),
            ("POST", "") => await OpenAsync(body, operatorName),
            ("DELETE", { Length: > 0 } id) => Close(id, operatorName),
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
                recording = _composer.Records,
                agents = _agents.Select(agent => new { value = agent.Value, label = agent.Label, featured = agent.Featured }).ToArray(),
                tables = await Task.WhenAll(_registry.All().Select(DescribedAsync)),
            },
            ArtifactJson.LineOptions);

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
        Console.WriteLine($"  Session {table.Id} opened by {operatorName}: {string.Join(", ", table.Seats.Select(seat => $"{seat.Name} {(seat.Person is null ? table.Session.Seat(seat.Slot).Seated.Name : $"code {_registry.Codes.Of(seat)}")}"))}");
        return StudioResponse.OfJson(await DescribedAsync(table), ArtifactJson.LineOptions, status: 201);
    }

    private StudioResponse Full() =>
        StudioResponse.OfJson(
            new { error = "Admin.Full", message = $"This host has {_registry.Capacity} tables under way, which is as many as it takes. Close one first." },
            ArtifactJson.LineOptions,
            status: 409);

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

                return StudioResponse.OfPlainText(404, $"No such route: {method} {SessionsPrefix}/{rest}");
        }
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
                link = seat.Person is null ? null : $"/?{seat.Name}={seat.Token}",
            }).ToArray(),

            // One link carrying every human seat's token, for the case where the two people share one
            // browser: the page follows whichever seat the match asks.
            hotseat = table.Seats.Count(seat => seat.Person is not null) > 1
                ? $"/?{string.Join('&', table.Seats.Where(seat => seat.Person is not null).Select(seat => $"{seat.Name}={seat.Token}"))}"
                : null,
            pilot = $"/pilot?token={table.Pilot.Token}",
            session = $"/session/{table.Id}",
        };

    /// <summary>
    /// The round a table is in, read without waiting long on it: a listing must not block behind a match that
    /// is in the middle of a command, so a table whose gate is busy reads as "somewhere" rather than holding
    /// the whole list. A read that failed -- the table was let go of underneath -- reads the same way.
    /// </summary>
    private static async Task<int?> RoundNowAsync(PlayedTable table)
    {
        var reading = table.RoundAsync();
        var first = await Task.WhenAny(reading, Task.Delay(TimeSpan.FromMilliseconds(200)));
        return first == reading && reading.IsCompletedSuccessfully ? reading.Result : null;
    }
}
