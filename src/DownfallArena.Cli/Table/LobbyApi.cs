using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Table;

/// <summary>The players' entrance: public summaries, explicitly open seats, and the seats a browser already holds.</summary>
internal sealed class LobbyApi(TableRegistry registry, TableComposer composer, TimeProvider clock, PushNotices? push = null)
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
                ("POST", Prefix + "/invitations") => await InvitationsAsync(body),
                ("POST", Prefix + "/claim") => await ClaimAsync(body),
                ("GET", Prefix + "/push") => Json(new { publicKey = push?.PublicKey }),
                ("PUT", Prefix + "/push") => await SubscribeAsync(token, body),
                ("DELETE", Prefix + "/push") => await SubscribeAsync(token, null),
                ("POST", Prefix + "/push/test") => await TestPushAsync(token),
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

            // The seats handed to this browser are taken by it from the start: no link names them, and only the
            // place left for another player keeps an invitation and a code (ADR 0116).
            var handed = table.Seats.Where(seat => seat.Person is not null && (seat.Name == "player1" || opponent == "hotseat")).ToArray();
            foreach (var seat in handed)
            {
                registry.Rekey(table, seat, SeatKeys.TakenBy(seat.Token));
            }

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
                seats = handed.Select(seat => new { seat = seat.Name, token = seat.Token }).ToArray(),
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

            var seat = table.Seats.FirstOrDefault(seat => table.OpenSeats.Contains(seat.Name) && seat.Person is { HasArrived: false } && !seat.Taken);
            if (seat is null)
            {
                return Problem(409, "Someone has already taken this place. Refresh the tables to find another.");
            }

            // The place is handed over under a fresh token, and its invitation and code are spent: they are in a
            // link and on the admin panel, and whoever holds those must not reach a seat someone else took.
            if (!await TakeAsync(table, [seat]))
            {
                return Problem(409, "This table is no longer available. Refresh the tables to find another.");
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

            // A fresh token revokes this browser, and a fresh invitation and code offer the place to the next
            // player (ADR 0116): the ones the seat was first offered under were spent when this player took it.
            var previous = registry.KeysOf(seat);
            var arrived = seat.Person!.HasArrived;
            var open = table.OpenSeats;
            if (!registry.Rekey(table, seat, SeatKeys.Offered()))
            {
                return Problem(409, "This table is no longer available.");
            }

            seat.Person.Depart();
            table.OpenSeats = [.. open.Append(seat.Name).Distinct(StringComparer.Ordinal)];
            try
            {
                await table.RecordAsync(registry.Codes);
            }
            catch
            {
                registry.Rekey(table, seat, previous);
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

    /// <summary>
    /// Where this seat's notices go from now on (ADR 0118), as the browser's <c>PushSubscription.toJSON()</c>
    /// gives it, or nowhere for a <c>DELETE</c>. The seat's token says which seat; a shared browser sends the same
    /// subscription for both of its seats. Written to the record before the answer, so the next host sends there too.
    /// </summary>
    private async Task<StudioResponse> SubscribeAsync(string? token, string? body)
    {
        if (push is null)
        {
            return Problem(404, "This host sends no notices: it has no push keys.");
        }

        PushSubscription? subscription = null;
        if (body is not null)
        {
            var sent = JsonSerializer.Deserialize<BrowserSubscription>(body, Web);
            subscription = PushSubscription.From(sent?.Endpoint, sent?.Keys?.P256dh, sent?.Keys?.Auth);
            if (subscription is null)
            {
                return Problem(400, "Send the subscription as the browser serialises it: an https endpoint, a p256dh key of 65 bytes and an auth secret of 16.");
            }

            if (push.IsDead(subscription.Endpoint))
            {
                return Problem(410, "This subscription is gone from its push service. Subscribe again.");
            }
        }

        // Under the claim gate, and the seat found under it: a leave, a claim or a new invitation replaces the
        // seat's keys, this subscription among them, and one landing between the lookup and the write would
        // hand the departed browser's phone to the next player's seat.
        await Claims.WaitAsync();
        try
        {
            var table = registry.ByToken(token);
            var seat = table?.Seats.FirstOrDefault(seat => seat.Token == token && seat.Person is not null);
            if (table is null || seat is null)
            {
                return Problem(403, "This seat is no longer yours.");
            }

            var before = seat.Push;
            seat.Push = subscription;
            try
            {
                await table.RecordAsync(registry.Codes);
            }
            catch
            {
                seat.Push = before;
                throw;
            }

            return Json(new { seat = seat.Name, notices = subscription is not null });
        }
        finally
        {
            Claims.Release();
        }
    }

    /// <summary>A notice to this seat's phone now, and what came of it: how a player sees that the phone can be reached.</summary>
    private async Task<StudioResponse> TestPushAsync(string? token)
    {
        if (push is null)
        {
            return Problem(404, "This host sends no notices: it has no push keys.");
        }

        var table = registry.ByToken(token);
        var seat = table?.Seats.FirstOrDefault(seat => seat.Token == token && seat.Person is not null);
        if (table is null || seat is null)
        {
            return Problem(403, "This seat is no longer yours.");
        }

        var test = await push.TestAsync(table, seat);
        return Json(new { sent = test.Sent, message = test.Message }, test.Sent ? 200 : 502);
    }

    /// <summary>
    /// What the keys a link carries are, before the page does anything with them (ADR 0116): an invitation still
    /// outstanding, a seat's own token (a link from before invitations, or a browser that keeps no storage), or
    /// nothing any more. Asking spends nothing, so a page can show what accepting would do and wait for a tap.
    /// </summary>
    private async Task<StudioResponse> InvitationsAsync(string body)
    {
        var tokens = JsonSerializer.Deserialize<ResumeRequest>(body, Web)?.Tokens;
        if (tokens is null || tokens.Count is 0 or > 2)
        {
            return Problem(400, "Send the one or two keys the link carries.");
        }

        var invitations = await Task.WhenAll(tokens.Distinct(StringComparer.Ordinal).Select(async token =>
        {
            if (registry.ByInvitation(token) is { Table.IsOver: false } offered)
            {
                return new { token, kind = "invitation", seat = (string?)offered.Seat.Name, table = (object?)await DescribeAsync(offered.Table) };
            }

            if (registry.ByToken(token) is { } table && table.Seats.FirstOrDefault(seat => seat.Token == token && seat.Person is not null) is { } held)
            {
                return new { token, kind = "seat", seat = (string?)held.Name, table = (object?)await DescribeAsync(table) };
            }

            // Spent, replaced, closed or never minted: which one is not told to whoever holds it.
            return new { token, kind = "spent", seat = (string?)null, table = (object?)null };
        }));
        return Json(new { invitations });
    }

    /// <summary>
    /// Accepts the invitations a link carries, one seat or both seats of one table: the first browser to ask takes
    /// the seat under a fresh token and the invitation is spent, so the same link opened again finds the place
    /// taken rather than a second key to it. All or none, and saved before the token is returned.
    /// </summary>
    private async Task<StudioResponse> ClaimAsync(string body)
    {
        var tokens = JsonSerializer.Deserialize<ResumeRequest>(body, Web)?.Tokens;
        if (tokens is null || tokens.Count is 0 or > 2)
        {
            return Problem(400, "Send the one or two invitations the link carries.");
        }

        await Claims.WaitAsync();
        try
        {
            var found = tokens.Distinct(StringComparer.Ordinal).Select(registry.ByInvitation).ToArray();
            if (found.Any(one => one is null) || found.Select(one => one!.Value.Table).Distinct().Count() != 1 || found[0]!.Value.Table.IsOver)
            {
                return Problem(409, "This invitation has already been used: someone has taken the place, or it was replaced. Ask the other player for a new code.");
            }

            var table = found[0]!.Value.Table;
            var seats = found.Select(one => one!.Value.Seat).ToArray();
            if (!await TakeAsync(table, seats))
            {
                return Problem(409, "This table is no longer available: it was closed.");
            }

            table.Touch(clock.GetUtcNow());
            return Json(new { table = await DescribeAsync(table), seats = seats.Select(seat => new { seat = seat.Name, token = seat.Token }).ToArray() });
        }
        finally
        {
            Claims.Release();
        }
    }

    /// <summary>
    /// Under the claim gate: the seats are this browser's now. Each gets a fresh token, its invitation and code
    /// are spent and it is no longer an open place, and the record says so before the token leaves the host.
    /// False, with nothing changed, when the table was let go of meanwhile.
    /// </summary>
    private async Task<bool> TakeAsync(PlayedTable table, TableSeat[] seats)
    {
        var before = seats.Select(registry.KeysOf).ToArray();
        void Undo()
        {
            for (var index = 0; index < seats.Length; index++)
            {
                registry.Rekey(table, seats[index], before[index]);
            }
        }

        if (!seats.All(seat => registry.Rekey(table, seat, SeatKeys.TakenBy(TableComposer.Token()))))
        {
            Undo();
            return false;
        }

        var open = table.OpenSeats;
        table.OpenSeats = [.. open.Where(name => seats.All(seat => seat.Name != name))];
        try
        {
            await table.RecordAsync(registry.Codes);
            return true;
        }
        catch
        {
            Undo();
            table.OpenSeats = open;
            throw;
        }
    }

    /// <summary>
    /// A seat token reaching an open place, which only a table recorded before invitations still has: its link
    /// carried the token itself, and the place is spent as it is reached, before its first board is served.
    /// </summary>
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

    private sealed record BrowserSubscription(string? Endpoint, BrowserKeys? Keys);

    private sealed record BrowserKeys(string? P256dh, string? Auth);
}
