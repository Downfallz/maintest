using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Table;

/// <summary>The players' entrance: public summaries, explicitly open seats, and the seats a browser already holds.</summary>
internal sealed class LobbyApi(TableRegistry registry, TableComposer composer, TimeProvider clock)
{
    public const string Prefix = "/api/lobby";

    // All entrances, including invitation links, spend a public place through the same gate. A claim is
    // durable before its token is returned, and a lobby claim rotates the seat's token and code, so two devices
    // cannot hold the same seat. Static because the admin's close and delete take it too, from AdminApi, and a
    // host has one of each; a seat request only takes it when its seat is still open (SeatReachedAsync).
    private static readonly SemaphoreSlim Claims = new(1, 1);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static bool Names(string path) => path == Prefix || path.StartsWith(Prefix + "/", StringComparison.Ordinal);

    /// <summary>An admin close or deletion must not race a claim writing the same table record.</summary>
    public static async Task<StudioResponse> ExclusivelyAsync(Func<Task<StudioResponse>> change)
    {
        await Claims.WaitAsync();
        try
        {
            return await change();
        }
        finally
        {
            Claims.Release();
        }
    }

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string? token = null)
    {
        try
        {
            return (method, path) switch
            {
                ("GET", Prefix) => await ListAsync(),
                ("POST", Prefix + "/resume") => await ResumeAsync(body),
                ("POST", Prefix + "/tables") => await OpenAsync(body),
                ("POST", Prefix + "/leave") => await LeaveAsync(token),
                ("POST", _) when JoinedTable(path) is { } id => await JoinAsync(id),
                _ => Problem(404, "No such lobby route."),
            };
        }
        catch (JsonException)
        {
            return Problem(400, "The request is not valid JSON.");
        }
    }

    /// <summary>The table id of <c>/api/lobby/tables/&lt;id&gt;/join</c>, or null for any other path, an empty id among them.</summary>
    private static string? JoinedTable(string path)
    {
        const string tables = Prefix + "/tables/";
        const string join = "/join";
        if (!path.StartsWith(tables, StringComparison.Ordinal) || !path.EndsWith(join, StringComparison.Ordinal) || path.Length <= tables.Length + join.Length)
        {
            return null;
        }

        var id = path[tables.Length..^join.Length];
        return id.Contains('/', StringComparison.Ordinal) ? null : id;
    }

    private async Task<StudioResponse> ListAsync() => Json(new
    {
        recording = composer.Records,
        format = composer.Rules.Format.ToString(),
        formats = MatchFormat.Offered.Select(format => format.ToString()).ToArray(),
        tables = await Task.WhenAll(registry.All().Where(table => !table.IsOver).Select(DescribeAsync)),
    });

    private async Task<StudioResponse> ResumeAsync(string body)
    {
        var tokens = JsonSerializer.Deserialize<ResumeRequest>(body, Web)?.Tokens;
        if (tokens is null || tokens.Count > 128)
        {
            return Problem(400, "Send at most 128 saved seat tokens.");
        }

        var held = tokens.Distinct(StringComparer.Ordinal)
            .Select(token => (Token: token, Table: registry.ByToken(token)))
            .Where(held => held.Table?.Seats.Any(seat => seat.Token == held.Token && seat.Person is not null) == true)
            .GroupBy(held => held.Table!);

        // Described together, as the listing is: each description may wait on its table's round.
        var tables = await Task.WhenAll(held.Select(async group => (object)new
        {
            table = await DescribeAsync(group.Key),
            seats = group.Select(one => new { seat = group.Key.Seats.Single(seat => seat.Token == one.Token).Name, token = one.Token }).ToArray(),
        }));
        return Json(new { tables });
    }

    private async Task<StudioResponse> OpenAsync(string body)
    {
        if (!composer.Records)
        {
            return Problem(409, "This host is not saving games. Ask the operator to enable recording before opening a table.");
        }

        var opening = JsonSerializer.Deserialize<Opening>(body, Web);
        var opponent = opening?.Opponent ?? "person";
        if (opponent is not ("person" or "hotseat" or "greedy" or "random"))
        {
            return Problem(400, "Choose another player, a shared device, Greedy or Random.");
        }

        var format = composer.Rules.Format;
        if (opening?.Format is { } named && (!MatchFormat.TryParse(named, out format) || !MatchFormat.Offered.Contains(format)))
        {
            return Problem(400, "Choose one of the offered match formats.");
        }

        await Claims.WaitAsync();
        try
        {
            using var reservation = registry.Reserve();
            if (reservation is null)
            {
                return Problem(409, "All tables are occupied. Ask the operator to close an unused table.");
            }

            var request = new TableRequest(null, opponent is "greedy" or "random" ? AgentSpec.Parse(opponent) : null, null, null, null, format);
            PlayedTable table;
            try
            {
                table = await composer.ComposeAsync(request);
            }
            catch (Exception failure) when (failure is ArgumentException or IOException or InvalidDataException or Azure.RequestFailedException)
            {
                // The same failures the admin panel answers as a refusal (AdminApi.OpenAsync): content that does
                // not load, or a store that is out. A visitor reads a sentence, not a 500.
                return Problem(503, $"The table could not be opened: {failure.Message}");
            }

            table.OpenSeats = opponent == "person" ? ["player2"] : [];
            registry.Add(table, reservation);
            try
            {
                await table.RecordAsync(registry.Codes);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or Azure.RequestFailedException)
            {
                // A game that cannot be saved is not offered: it would be lost on the next redeploy.
                registry.Remove(table.Id);
                return Problem(503, $"The table could not be saved, so it was not opened: {failure.Message}");
            }

            return Json(new
            {
                table = await DescribeAsync(table),
                seats = table.Seats.Where(seat => seat.Name == "player1" || opponent == "hotseat").Select(seat => new { seat = seat.Name, token = seat.Token }).ToArray(),
            }, 201);
        }
        finally
        {
            Claims.Release();
        }
    }

    private async Task<StudioResponse> JoinAsync(string id)
    {
        await Claims.WaitAsync();
        try
        {
            if (registry.ById(id) is not { IsOver: false } table)
            {
                return Problem(409, "This table is no longer available. Refresh the tables to find another.");
            }

            var seat = table.Seats.FirstOrDefault(seat => table.OpenSeats.Contains(seat.Name) && seat.Person is { HasArrived: false });
            if (seat is null)
            {
                return Problem(409, "Someone has already taken this place. Refresh the tables to find another.");
            }

            // The place is handed over under a fresh token and code: the ones the seat had are in its invitation
            // link and on the admin panel, and whoever holds those must not reach a seat someone else took.
            var previous = seat.Token;
            var code = registry.Codes.Of(seat);
            registry.ReplaceToken(table, seat, TableComposer.Token());
            try
            {
                await ReserveSeatAsync(table, seat.Name);
            }
            catch
            {
                registry.ReplaceToken(table, seat, previous, code);
                throw;
            }

            table.Touch(clock.GetUtcNow());
            return Json(new { table = await DescribeAsync(table), seats = new[] { new { seat = seat.Name, token = seat.Token } } });
        }
        finally
        {
            Claims.Release();
        }
    }

    private async Task<StudioResponse> LeaveAsync(string? token)
    {
        await Claims.WaitAsync();
        try
        {
            var table = registry.ByToken(token);
            var seat = table?.Seats.FirstOrDefault(seat => seat.Token == token && seat.Person is not null);
            if (table is null || seat is null)
            {
                return Problem(403, "This seat is no longer yours.");
            }

            if (table.IsOver || table.Run is null)
            {
                return Problem(409, "Only a saved, unfinished game can offer a place to another player.");
            }

            var previous = seat.Token;
            var code = registry.Codes.Of(seat);
            var arrived = seat.Person!.HasArrived;
            var open = table.OpenSeats;
            registry.ReplaceToken(table, seat, TableComposer.Token());
            seat.Person.Depart();
            table.OpenSeats = [.. open.Append(seat.Name).Distinct(StringComparer.Ordinal)];
            try
            {
                await table.RecordAsync(registry.Codes);
            }
            catch
            {
                registry.ReplaceToken(table, seat, previous, code);
                table.OpenSeats = open;
                if (arrived)
                {
                    seat.Person.Arrive();
                }

                throw;
            }

            return Json(new { left = true });
        }
        finally
        {
            Claims.Release();
        }
    }

    /// <summary>An invitation consumes the same place as Join, before its first board is served.</summary>
    public async Task SeatReachedAsync(PlayedTable table, string? token)
    {
        ArgumentNullException.ThrowIfNull(table);

        // Every poll of every table comes through here. Only a seat that is still open has a place to spend, so
        // every other request goes on without waiting behind a claim, a table being opened or an admin delete.
        if (!table.Seats.Any(seat => seat.Token == token && table.OpenSeats.Contains(seat.Name)))
        {
            return;
        }

        await Claims.WaitAsync();
        try
        {
            var seat = table.Seats.FirstOrDefault(seat => seat.Token == token && table.OpenSeats.Contains(seat.Name));
            if (seat is not null && !table.IsOver)
            {
                await ReserveSeatAsync(table, seat.Name);
            }
        }
        finally
        {
            Claims.Release();
        }
    }

    private async Task ReserveSeatAsync(PlayedTable table, string name)
    {
        var before = table.OpenSeats;
        table.OpenSeats = [.. before.Where(open => open != name)];
        try
        {
            await table.RecordAsync(registry.Codes);
        }
        catch
        {
            table.OpenSeats = before;
            throw;
        }
    }

    private static async Task<object> DescribeAsync(PlayedTable table) => new
    {
        id = table.Id,
        createdAt = table.CreatedAt,
        format = table.Session.Rules.Format.ToString(),
        waiting = !table.Session.HasBegun && !table.IsOver,
        over = table.IsOver,
        round = await AdminApi.RoundNowAsync(table),
        openSeats = table.IsOver ? 0 : table.Seats.Count(seat => table.OpenSeats.Contains(seat.Name) && seat.Person is { HasArrived: false }),
        players = table.Seats.Select(seat => seat.Person is null ? table.Session.Seat(seat.Slot).Seated.Name : "Player").ToArray(),
    };

    private static StudioResponse Json(object value, int status = 200) => StudioResponse.OfJson(value, ArtifactJson.LineOptions, status);

    private static StudioResponse Problem(int status, string message) => Json(new { message }, status);

    private sealed record Opening(string? Opponent, string? Format);

    private sealed record ResumeRequest(IReadOnlyList<string>? Tokens);
}
