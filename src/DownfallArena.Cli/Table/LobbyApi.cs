using DownfallArena.Cli.Studio;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The operator's routes: the tables this host is playing, and opening a new one (ADR 0081). Everything here
/// is behind <see cref="OperatorGate" />: the players never come here, and a request that is nobody's is
/// answered with where to sign in.
/// </summary>
/// <remarks>
/// What a table answers with is what its console line used to say: each seat's code and link, the pilot's
/// link, and where the session is being written. The tokens are in it because the operator is the one person
/// they are for -- the console printed them before, and the lobby is the console of a host that has none.
/// </remarks>
internal sealed class LobbyApi
{
    public const string Prefix = "/api/tables";

    private readonly TableRegistry _registry;
    private readonly TableComposer _composer;
    private readonly OperatorGate _gate;
    private readonly TimeProvider _clock;
    private readonly string _rules;

    /// <param name="rules">The line the console prints of the rule set: which one, and where it came from.</param>
    public LobbyApi(TableRegistry registry, TableComposer composer, OperatorGate gate, TimeProvider clock, string rules)
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
    }

    public static bool Names(string path) => path == Prefix || path.StartsWith(Prefix + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string? token, string? principal)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_gate.Admit(token, principal) is not { } operatorName)
        {
            return Refused();
        }

        // The one moment memory is about to be spent, so the one moment tables nobody is at are let go of.
        foreach (var gone in _registry.Sweep(_clock.GetUtcNow()))
        {
            Console.WriteLine($"  Session {gone} let go of: nobody has asked for it in a while.");
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
            ? StudioResponse.OfJson(new { error = "Lobby.SignIn", message = "Sign in to open a table.", login = OperatorGate.Login }, ArtifactJson.LineOptions, status: 401)
            : StudioResponse.OfJson(new { error = "Lobby.Token", message = $"The lobby carries the operator's token the console printed, as '{TableApi.TokenHeader}'." }, ArtifactJson.LineOptions, status: 403);

    private async Task<StudioResponse> ListAsync() =>
        StudioResponse.OfJson(
            new
            {
                rules = _rules,
                recording = _composer.Records,
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
        catch (Exception failure) when (failure is ArgumentException or IOException or System.Text.Json.JsonException or InvalidDataException)
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
            new { error = "Lobby.Full", message = $"This host has {_registry.Capacity} tables under way, which is as many as it takes. Close one first." },
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

    /// <summary>One table as the lobby shows it: where it is, who is in each seat, and how to reach it.</summary>
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
