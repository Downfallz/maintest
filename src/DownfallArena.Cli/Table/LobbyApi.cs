using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;

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

    /// <summary>
    /// The players' sign-in (ADR 0122), when this host has one. With it, playing needs an account: opening a table,
    /// taking a place and accepting an invitation are refused to nobody, and every seat handed out belongs to the
    /// account that took it.
    /// </summary>
    public AccountApi? Accounts { get; set; }

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

    /// <param name="player">Who the request's session says is signed in, when this host signs players in.</param>
    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string? token = null, SignedIn? player = null)
    {
        var account = player?.Account.Id;
        try
        {
            return (method, path) switch
            {
                ("GET", Prefix) => await ListAsync(),
                // Signed out where players sign in, a saved seat is not "nothing": it is a seat that needs its player
                // signed in, and a page told nothing would forget it instead of sending them to sign in.
                ("POST", Prefix + "/resume") => SignInRequired(account) ?? await ResumeAsync(body, account),
                ("GET", Prefix + "/mine") => await MineAsync(account),
                ("POST", Prefix + "/adopt") => await AdoptAsync(body, account),
                ("POST", Prefix + "/tables") => SignInRequired(account) ?? await OpenAsync(body, account),
                ("POST", Prefix + "/leave") => await LeaveAsync(token, account),
                ("POST", Prefix + "/invitations") => await InvitationsAsync(body),
                ("POST", Prefix + "/claim") => SignInRequired(account) ?? await ClaimAsync(body, account),
                ("GET", Prefix + "/push") => Json(new { publicKey = push?.PublicKey }),
                ("PUT", Prefix + "/push") => await SubscribeAsync(token, body, account),
                ("DELETE", Prefix + "/push") => await SubscribeAsync(token, null, account),
                ("POST", Prefix + "/push/test") => await TestPushAsync(token, account),
                ("POST", _) when JoinedTable(path) is { } id => SignInRequired(account) ?? await JoinAsync(id, account),
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
        accounts = AccountApi.Named(Accounts?.Mode ?? AccountsMode.Off),
        tables = await Task.WhenAll(registry.All().Where(table => !table.IsOver).Select(DescribeAsync)),
    });

    private async Task<StudioResponse> ResumeAsync(string body, AccountId? account)
    {
        var tokens = JsonSerializer.Deserialize<ResumeRequest>(body, Web)?.Tokens;
        if (tokens is null || tokens.Count > 128)
        {
            return Problem(400, "Send at most 128 saved seat tokens.");
        }

        var held = tokens.Distinct(StringComparer.Ordinal)
            .Select(token => (Token: token, Table: registry.ByToken(token)))
            .Where(held => held.Table?.Seats.Any(seat => seat.Token == held.Token && seat.Person is not null && Owns(seat, account)) == true)
            .GroupBy(held => held.Table!);

        // Described together, as the listing is: each description may wait on its table's round.
        var tables = await Task.WhenAll(held.Select(async group => (object)new
        {
            table = await DescribeAsync(group.Key),
            seats = group.Select(one => new { seat = group.Key.Seats.Single(seat => seat.Token == one.Token).Name, token = one.Token }).ToArray(),
        }));
        return Json(new { tables });
    }

    /// <summary>
    /// The one bot a visitor may play (ADR 0121): the first one <c>learning/seatable.json</c> puts forward, which is
    /// the strongest the journal has measured, so the lobby follows the next one without a change here. Greedy when
    /// the host was given no list.
    /// </summary>
    private string Strongest => composer.Agents.FirstOrDefault(agent => agent.Featured)?.Value ?? "greedy";

    private async Task<StudioResponse> OpenAsync(string body, AccountId? account)
    {
        if (!composer.Records)
        {
            return Problem(409, "This host is not saving games. Ask the operator to enable recording before opening a table.");
        }

        var opening = JsonSerializer.Deserialize<Opening>(body, Web);
        var opponent = opening?.Opponent ?? "person";
        if (opponent is not ("person" or "hotseat" or "bot"))
        {
            return Problem(400, "Choose another player, a shared device, or the bot.");
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

            var request = new TableRequest(null, opponent == "bot" ? AgentSpec.Parse(Strongest) : null, null, null, null, format);
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
                registry.Rekey(table, seat, SeatKeys.TakenBy(seat.Token, account));
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

    private async Task<StudioResponse> JoinAsync(string id, AccountId? account)
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

            // Both sides of one online table are two people; a player who wants both opens a shared-device table.
            if (account is not null && table.Seats.Any(held => held.Account == account))
            {
                return Problem(409, "You already play at this table. Open it from Your games.");
            }

            // The place is handed over under a fresh token, and its invitation and code are spent: they are in a
            // link and on the admin panel, and whoever holds those must not reach a seat someone else took.
            if (!await TakeAsync(table, [seat], account))
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

    private async Task<StudioResponse> LeaveAsync(string? token, AccountId? account)
    {
        await Claims.WaitAsync();
        try
        {
            var table = registry.ByToken(token);
            var seat = table?.Seats.FirstOrDefault(seat => seat.Token == token && seat.Person is not null && Owns(seat, account));
            if (table is null || seat is null)
            {
                return Problem(403, "This seat is no longer yours.");
            }

            if (table.IsOver || table.Run is null)
            {
                return Problem(409, "Only a saved, unfinished game can offer a place to another player.");
            }

            return await VacateAsync(table, seat)
                ? Json(new { left = true })
                : Problem(409, "This table is no longer available.");
        }
        finally
        {
            Claims.Release();
        }
    }

    /// <summary>
    /// Under the claim gate: the seat is offered to the next player. A fresh token revokes the browser that held it,
    /// and a fresh invitation and code offer the place (ADR 0116): the ones the seat was first offered under were
    /// spent when its player took it. Saved before it counts; false, with nothing changed, once the table is gone.
    /// </summary>
    private async Task<bool> VacateAsync(PlayedTable table, TableSeat seat)
    {
        var previous = registry.KeysOf(seat);
        var arrived = seat.Person!.HasArrived;
        var open = table.OpenSeats;
        if (!registry.Rekey(table, seat, SeatKeys.Offered()))
        {
            return false;
        }

        seat.Person.Depart();
        table.OpenSeats = [.. open.Append(seat.Name).Distinct(StringComparer.Ordinal)];
        try
        {
            await table.RecordAsync(registry.Codes);
            table.Api.Changes.Bump();
            return true;
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
    }

    /// <summary>
    /// What a deleted account leaves behind at the tables (ADR 0122): every seat it holds in a saved, unfinished
    /// game is offered to another player, as leaving does, and a seat at an unsaved one simply belongs to nobody
    /// now. A finished table is history and keeps the id it was played under, as its recording and its result do;
    /// a card names it "Player", since the account is gone. The number of seats offered.
    /// </summary>
    public async Task<int> ReleaseAsync(AccountId account)
    {
        await Claims.WaitAsync();
        try
        {
            var offered = 0;
            foreach (var table in registry.All())
            {
                foreach (var seat in table.Seats.Where(seat => seat.Account == account && seat.Person is not null && !table.IsOver).ToArray())
                {
                    if (table.Run is not null && await VacateAsync(table, seat))
                    {
                        offered++;
                    }
                    else
                    {
                        seat.Account = null;
                    }
                }
            }

            return offered;
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
    private async Task<StudioResponse> SubscribeAsync(string? token, string? body, AccountId? account)
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
            var seat = table?.Seats.FirstOrDefault(seat => seat.Token == token && seat.Person is not null && Owns(seat, account));
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
    private async Task<StudioResponse> TestPushAsync(string? token, AccountId? account)
    {
        if (push is null)
        {
            return Problem(404, "This host sends no notices: it has no push keys.");
        }

        var table = registry.ByToken(token);
        var seat = table?.Seats.FirstOrDefault(seat => seat.Token == token && seat.Person is not null && Owns(seat, account));
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
    private async Task<StudioResponse> ClaimAsync(string body, AccountId? account)
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
            if (!await TakeAsync(table, seats, account))
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
    private async Task<bool> TakeAsync(PlayedTable table, TableSeat[] seats, AccountId? account)
    {
        var before = seats.Select(registry.KeysOf).ToArray();
        void Undo()
        {
            for (var index = 0; index < seats.Length; index++)
            {
                registry.Rekey(table, seats[index], before[index]);
            }
        }

        if (!seats.All(seat => registry.Rekey(table, seat, SeatKeys.TakenBy(TableComposer.Token(), account))))
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

    private async Task<object> DescribeAsync(PlayedTable table)
    {
        var seats = await Task.WhenAll(table.Seats.Select(seat => SeatAsync(table, seat)));
        return new
        {
            id = table.Id,
            createdAt = table.CreatedAt,
            format = table.Session.Rules.Format.ToString(),
            waiting = !table.Session.HasBegun && !table.IsOver,
            over = table.IsOver,
            round = await AdminApi.RoundNowAsync(table),
            openSeats = table.IsOver ? 0 : table.Seats.Count(seat => table.OpenSeats.Contains(seat.Name) && seat.Person is { HasArrived: false }),
            players = seats.Select(seat => seat.Name).ToArray(),
            seats,
        };
    }

    /// <summary>
    /// Who sits in a seat, as a card says it (ADR 0122): a person by the name their account chose, a bot by its
    /// own, an open place as open. A person's seat that belongs to no account (a host without accounts, or a
    /// table older than them) is "Player", as it always was.
    /// </summary>
    private async Task<SeatSummary> SeatAsync(PlayedTable table, TableSeat seat)
    {
        if (seat.Person is null)
        {
            // The name the bot list gives it (search-19, Greedy), not the spec it was seated by, which is a path.
            var spec = table.Session.Seat(seat.Slot).Seated.Name;
            var label = composer.Agents.FirstOrDefault(agent => string.Equals(agent.Value, spec, StringComparison.OrdinalIgnoreCase))?.Label;
            return new SeatSummary(seat.Name, "bot", BotName(label ?? spec), Turn: false);
        }

        // Whose turn it is: a seat whose person is being asked a question right now. Both seats can be at once
        // (ADR 0092, 0111); a bot's is never, since a bot answers as it is asked.
        var turn = !table.IsOver && seat.Person.Waiting is not null;
        if (!table.IsOver && table.OpenSeats.Contains(seat.Name) && !seat.Taken)
        {
            return new SeatSummary(seat.Name, "open", "Open seat", turn);
        }

        var name = seat.Account is { } id && Accounts is { } accounts ? (await accounts.AccountAsync(id))?.Username.Value : null;
        return new SeatSummary(seat.Name, "person", name ?? "Player", turn);
    }

    /// <summary>
    /// A bot's name for a card: its label up to the description the bot list adds after a dash ("Lookahead
    /// (lookahead-55) — the strongest: ..."), or a spec's agent kind when there is no label ("greedy").
    /// </summary>
    internal static string BotName(string label)
    {
        var dash = label.IndexOf(" — ", StringComparison.Ordinal);
        var name = dash > 0 ? label[..dash] : label;
        var kind = name.IndexOf(':', StringComparison.Ordinal);
        return name.Contains('/', StringComparison.Ordinal) && kind > 0 ? name[..kind] : name;
    }

    /// <summary>Whether a request may act for a seat: any seat on a host without accounts, and otherwise only one that belongs to nobody or to it.</summary>
    private bool Owns(TableSeat seat, AccountId? account) => Accounts is null || seat.Account is null || seat.Account == account;

    /// <summary>The refusal a host with accounts gives a request that is nobody's; null where it may go on.</summary>
    private StudioResponse? SignInRequired(AccountId? account) =>
        Accounts is not null && account is null
            ? StudioResponse.OfJson(new { error = "Account.SignInRequired", message = "Sign in to play: open the home page and continue with Google." }, ArtifactJson.LineOptions, 401)
            : null;

    /// <summary>
    /// The tables a signed-in player has seats at, on any device (ADR 0122): what "Your games" lists, with the
    /// tokens this browser plays them by. Finished ones are kept for the hour a host keeps them, as on a browser.
    /// </summary>
    private async Task<StudioResponse> MineAsync(AccountId? account)
    {
        if (account is null)
        {
            return Json(new { tables = Array.Empty<object>() });
        }

        var tables = await Task.WhenAll(registry.All()
            .Where(table => table.Seats.Any(seat => seat.Account == account))
            .Select(async table => (object)new
            {
                table = await DescribeAsync(table),
                seats = table.Seats.Where(seat => seat.Account == account).Select(seat => new { seat = seat.Name, token = seat.Token }).ToArray(),
            }));
        return Json(new { tables });
    }

    /// <summary>
    /// Seats a browser saved before its player signed in, made the account's (ADR 0122): a seat its token reaches
    /// that belongs to nobody yet. A seat another account holds is left as it is. Answered as <see cref="MineAsync" />.
    /// </summary>
    private async Task<StudioResponse> AdoptAsync(string body, AccountId? account)
    {
        if (account is not { } owner)
        {
            return SignInRequired(account) ?? Problem(404, "Players do not sign in at this host.");
        }

        var tokens = JsonSerializer.Deserialize<ResumeRequest>(body, Web)?.Tokens;
        if (tokens is null || tokens.Count > 128)
        {
            return Problem(400, "Send at most 128 saved seat tokens.");
        }

        foreach (var token in tokens.Distinct(StringComparer.Ordinal))
        {
            if (registry.ByToken(token) is { IsOver: false } table && table.Seats.FirstOrDefault(seat => seat.Token == token && seat.Person is not null) is { Account: null } seat)
            {
                await BindAsync(table, seat, owner);
            }
        }

        return await MineAsync(owner);
    }

    /// <summary>
    /// Makes a seat nobody owns the account's, under the claim gate, and saved before it counts: what a seat reached
    /// by its token alone becomes once its player has signed in. False when somebody else got it first.
    /// </summary>
    public async Task<bool> BindAsync(PlayedTable table, TableSeat seat, AccountId account)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(seat);
        await Claims.WaitAsync();
        try
        {
            if (seat.Account is { } held)
            {
                return held == account;
            }

            seat.Account = account;
            try
            {
                await table.RecordAsync(registry.Codes);
                return true;
            }
            catch
            {
                seat.Account = null;
                throw;
            }
        }
        finally
        {
            Claims.Release();
        }
    }

    private static StudioResponse Json(object value, int status = 200) => StudioResponse.OfJson(value, ArtifactJson.LineOptions, status);

    private static StudioResponse Problem(int status, string message) => Json(new { message }, status);

    private sealed record Opening(string? Opponent, string? Format);

    /// <param name="Turn">Whether this seat's player is being asked something now: whose turn it is.</param>
    private sealed record SeatSummary(string Seat, string Kind, string Name, bool Turn);

    private sealed record ResumeRequest(IReadOnlyList<string>? Tokens);

    private sealed record BrowserSubscription(string? Endpoint, BrowserKeys? Keys);

    private sealed record BrowserKeys(string? P256dh, string? Auth);
}
