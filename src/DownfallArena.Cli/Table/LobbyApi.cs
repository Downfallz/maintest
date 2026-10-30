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
    // durable before its token is returned, so two devices cannot acquire the same seat.
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
            if (method == "GET" && path == Prefix)
            {
                return Json(new
                {
                    recording = composer.Records,
                    format = composer.Rules.Format.ToString(),
                    formats = MatchFormat.Offered.Select(format => format.ToString()).ToArray(),
                    tables = await Task.WhenAll(registry.All().Where(table => !table.IsOver).Select(DescribeAsync)),
                });
            }

            if (method == "POST" && path == Prefix + "/resume")
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
                var tables = new List<object>();
                foreach (var group in held)
                {
                    tables.Add(new
                    {
                        table = await DescribeAsync(group.Key),
                        seats = group.Select(held => new { seat = group.Key.Seats.Single(seat => seat.Token == held.Token).Name, token = held.Token }).ToArray(),
                    });
                }

                return Json(new { tables });
            }

            if (method == "POST" && path == Prefix + "/tables")
            {
                return await OpenAsync(body);
            }

            if (method == "POST" && path == Prefix + "/leave")
            {
                return await LeaveAsync(token);
            }

            if (method == "POST" && path.StartsWith(Prefix + "/tables/", StringComparison.Ordinal) && path.EndsWith("/join", StringComparison.Ordinal))
            {
                var id = path[(Prefix + "/tables/").Length..^"/join".Length];
                return await JoinAsync(id);
            }

            return Problem(404, "No such lobby route.");
        }
        catch (JsonException)
        {
            return Problem(400, "The request is not valid JSON.");
        }
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
            var table = await composer.ComposeAsync(request);
            table.OpenSeats = opponent == "person" ? ["player2"] : [];
            registry.Add(table, reservation);
            try
            {
                await table.RecordAsync(registry.Codes);
            }
            catch
            {
                registry.Remove(table.Id);
                throw;
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

            await ReserveSeatAsync(table, seat.Name);
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
