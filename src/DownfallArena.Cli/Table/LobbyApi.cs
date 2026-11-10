using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
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

    /// <summary>The friendships (ADR 0129), when this host has accounts: who a table may be opened for.</summary>
    public Friends? Friends { get; set; }

    /// <summary>Where finished games are kept (ADR 0147), the composer's own unless given: what the Finished tab reads.</summary>
    public Application.Accounts.Ports.IMatchResults? Results { get; set; }

    /// <summary>
    /// A finished session's replay off the host's store, as the admin panel serves it (ADR 0156); none on a host that
    /// records nothing.
    /// </summary>
    public Func<string, Task<StudioResponse>>? Replay { get; set; }

    /// <summary>What tells a friend's devices of an invitation, and the inviter's of its answer; none without push keys.</summary>
    public AccountNotices? Notices { get; set; }

    /// <summary>
    /// How many games an account may have in progress against bots, and how many against players (ADR 0147). A sixth
    /// is refused until one ends, so a player's lobby stays a list they can read.
    /// </summary>
    public const int OngoingPerKind = 5;

    /// <summary>How many games in progress one page of Find an opponent shows.</summary>
    public const int PageSize = 8;

    /// <summary>How long a seat is kept for a friend (ADR 0129): past this, the invitation counts as declined.</summary>
    public static readonly TimeSpan InvitationFor = TimeSpan.FromHours(24);

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
                ("GET", _) when FinishedRoute(path) is { } finished => SignInRequired(account) ?? await FinishedAsync(finished.AgainstBots, finished.Page, account),
                ("GET", Prefix + "/opponents") => SignInRequired(account) ?? await OpponentsAsync(account),
                ("GET", _) when PageRoute(path) is { } listed => await PageAsync(listed.OpenOnly, listed.Page, account),
                ("POST", Prefix + "/adopt") => await AdoptAsync(body, account),
                ("POST", Prefix + "/tables") => SignInRequired(account) ?? await OpenAsync(body, account),
                ("POST", Prefix + "/leave") => await LeaveAsync(token, account),
                ("POST", Prefix + "/invitations") => await InvitationsAsync(body, account),
                ("POST", Prefix + "/claim") => SignInRequired(account) ?? await ClaimAsync(body, account),
                ("GET", Prefix + "/push") => Json(new { publicKey = push?.PublicKey }),
                ("PUT", Prefix + "/push") => await SubscribeAsync(token, body, account),
                ("DELETE", Prefix + "/push") => await SubscribeAsync(token, null, account),
                ("POST", Prefix + "/push/test") => await TestPushAsync(token, account),
                ("POST", _) when TableRoute(path, "/join") is { } id => SignInRequired(account) ?? await JoinAsync(id, account),
                // A seat kept for a friend (ADR 0129): accepted or declined by that friend, cancelled, given to another
                // friend or opened to everyone by the inviter; and an unbegun table closed by whoever holds its seats.
                ("POST", _) when TableRoute(path, "/accept") is { } id => SignInRequired(account) ?? await AcceptAsync(id, player!),
                ("POST", _) when TableRoute(path, "/decline") is { } id => SignInRequired(account) ?? await DeclineAsync(id, player!),
                ("DELETE", _) when TableRoute(path, "/invitation") is { } id => SignInRequired(account) ?? await CancelAsync(id, player!),
                ("POST", _) when TableRoute(path, "/invitation") is { } id => SignInRequired(account) ?? await ReinviteAsync(id, body, player!),
                ("POST", _) when TableRoute(path, "/open") is { } id => SignInRequired(account) ?? await OpenToEveryoneAsync(id, player!),
                ("DELETE", _) when TableRoute(path, "") is { } id => SignInRequired(account) ?? await CloseAsync(id, player!),
                ("GET", _) when ReplayRoute(path) is { } id => await ReplayAsync(id, account),
                _ => Problem(404, "No such lobby route."),
            };
        }
        catch (JsonException)
        {
            return Problem(400, "The request is not valid JSON.");
        }
    }

    /// <summary>How many seats of a table in progress anybody may take: open places nobody has reached yet.</summary>
    private static int OpenSeatCount(PlayedTable table) =>
        table.IsOver ? 0 : table.Seats.Count(seat => table.OpenSeats.Contains(seat.Name) && seat.Person is { HasArrived: false });

    /// <summary>
    /// Whether <c>/api/lobby/page/open/&lt;page&gt;</c> or <c>/api/lobby/page/all/&lt;page&gt;</c> was asked, and
    /// which page, counted from 1; null for any other path.
    /// </summary>
    private static (bool OpenOnly, int Page)? PageRoute(string path)
    {
        const string listed = Prefix + "/page/";
        if (!path.StartsWith(listed, StringComparison.Ordinal) || path[listed.Length..].Split('/') is not [var view, var page])
        {
            return null;
        }

        return view is "open" or "all" && int.TryParse(page, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && number >= 1
            ? (view == "open", number - 1)
            : null;
    }

    /// <summary>
    /// One page of the games in progress, newest first, as the lobby's Find an opponent lists them: the ones with a
    /// seat to take, or all of them. A signed-in player's own tables are not offered to them as open seats; they are
    /// under Your games. A page past the end shows the last one.
    /// </summary>
    private async Task<StudioResponse> PageAsync(bool openOnly, int page, AccountId? account)
    {
        PlayedTable[] tables = [.. registry.All()
            .Where(table => !table.IsOver)
            .Where(table => !openOnly || (OpenSeatCount(table) > 0 && (account is not { } me || table.Seats.All(seat => seat.Account != me))))
            .OrderByDescending(table => table.CreatedAt)
            .ThenBy(table => table.Id, StringComparer.Ordinal)];
        var pages = Math.Max(1, (tables.Length + PageSize - 1) / PageSize);
        var at = Math.Clamp(page, 0, pages - 1);
        var items = await Task.WhenAll(tables.Skip(at * PageSize).Take(PageSize).Select(DescribeAsync));
        return Json(new { total = tables.Length, pages, page = at + 1, items });
    }

    /// <summary>
    /// The kind and page of <c>/api/lobby/finished/bots/&lt;page&gt;</c> or <c>/api/lobby/finished/players/&lt;page&gt;</c>,
    /// pages counted from 1; null for any other path.
    /// </summary>
    private static (bool AgainstBots, int Page)? FinishedRoute(string path)
    {
        const string finished = Prefix + "/finished/";
        if (!path.StartsWith(finished, StringComparison.Ordinal) || path[finished.Length..].Split('/') is not [var kind, var page])
        {
            return null;
        }

        return kind is "bots" or "players" && int.TryParse(page, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && number >= 1
            ? (kind == "bots", number - 1)
            : null;
    }

    /// <summary>The table id of <c>/api/lobby/tables/&lt;id&gt;&lt;suffix&gt;</c>, or null for any other path, an empty id among them.</summary>
    /// <summary>The table of <c>/api/lobby/tables/&lt;table&gt;/replay</c>, or null for any other path.</summary>
    internal static string? ReplayRoute(string path) => TableRoute(path, "/replay");

    private static string? TableRoute(string path, string suffix)
    {
        const string tables = Prefix + "/tables/";
        if (!path.StartsWith(tables, StringComparison.Ordinal) || !path.EndsWith(suffix, StringComparison.Ordinal) || path.Length <= tables.Length + suffix.Length)
        {
            return null;
        }

        var id = path[tables.Length..^suffix.Length];
        return id.Contains('/', StringComparison.Ordinal) ? null : id;
    }

    private async Task<StudioResponse> ListAsync() => Json(new
    {
        recording = composer.Records,
        format = composer.Rules.Format.ToString(),
        formats = MatchFormat.Offered.Select(format => format.ToString()).ToArray(),
        accounts = AccountApi.Named(Accounts?.Mode ?? AccountsMode.Off),
        // The tables themselves are read a page at a time (PageAsync): a lobby polled every ten seconds by every
        // player must not carry every game in progress each time.
        ongoing = registry.All().Count(table => !table.IsOver),
        open = registry.All().Count(table => !table.IsOver && OpenSeatCount(table) > 0),
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
        if (opponent is not ("person" or "hotseat" or "bot" or "friend"))
        {
            return Problem(400, "Choose another player, a friend, a shared device, or the bot.");
        }

        // A table for a friend (ADR 0129): the second seat is kept for that account, and one such seat at a time.
        // Checked under the claim gate below: two requests of one account must not both pass the one-at-a-time rule.
        var friend = opponent == "friend" ? opening?.Friend : null;

        var format = composer.Rules.Format;
        if (opening?.Format is { } named && (!MatchFormat.TryParse(named, out format) || !MatchFormat.Offered.Contains(format)))
        {
            return Problem(400, "Choose one of the offered match formats.");
        }

        await Claims.WaitAsync();
        try
        {
            if (opponent == "friend" && await FriendRefusalAsync(account, friend) is { } refused)
            {
                return refused;
            }

            if (TooManyOngoing(account, againstBot: opponent == "bot") is { } crowded)
            {
                return crowded;
            }

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
            table.Casual = opponent is "person" or "friend" && opening?.Ranked == false;
            registry.Add(table, reservation);

            // The seats handed to this browser are taken by it from the start: no link names them, and only the
            // place left for another player keeps an invitation and a code (ADR 0116).
            var handed = table.Seats.Where(seat => seat.Person is not null && (seat.Name == "player1" || opponent == "hotseat")).ToArray();
            foreach (var seat in handed)
            {
                registry.Rekey(table, seat, SeatKeys.TakenBy(seat.Token, account));
            }

            if (friend is not null)
            {
                registry.Rekey(table, table.Seats[1], SeatKeys.KeptFor(AccountId.Parse(friend), clock.GetUtcNow()));
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

            if (friend is not null)
            {
                await InviteNoticeAsync(AccountId.Parse(friend), account!.Value, table);
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

    /// <summary>
    /// Why a seat cannot be kept for that friend, or null: this host has no friends, the id is not one, they are
    /// not a friend, or the asker already has an invitation waiting (ADR 0129). Expired invitations are let go of
    /// first, so one nobody answered a day ago does not stand in the way. Under the claim gate, as its callers are.
    /// </summary>
    private async Task<StudioResponse?> FriendRefusalAsync(AccountId? account, string? friendId)
    {
        if (Friends is not { } friends || account is not { } me)
        {
            return Problem(404, "Lobby.NoFriends", "Players do not keep friends at this host.");
        }

        if (!AccountId.TryParse(friendId, out var friend) || friend == me)
        {
            return Problem(400, "Lobby.NoSuchFriend", "Choose a friend from your list.");
        }

        if (!await friends.AreFriendsAsync(me, friend))
        {
            return Problem(403, FriendErrors.NotFriends.Code, FriendErrors.NotFriends.Message);
        }

        await ExpireInvitationsAsync();
        return HasOutstandingInvitation(me)
            ? Problem(409, "Lobby.InvitationOutstanding", "You already have an invitation waiting for an answer. Cancel it first, or wait for it.")
            : null;
    }

    /// <summary>Whether the account holds a table with a seat still kept for a friend: one such at a time (ADR 0129).</summary>
    private bool HasOutstandingInvitation(AccountId me) =>
        registry.All().Any(table => !table.IsOver && Inviter(table, me) && table.Seats.Any(seat => seat.InvitedAccount is not null && !seat.Taken));

    /// <summary>Whether the account holds a taken seat at the table: who may cancel, re-invite, open or close it.</summary>
    private static bool Inviter(PlayedTable table, AccountId me) =>
        table.Seats.Any(seat => seat.Person is not null && seat.Taken && seat.Account == me);

    /// <summary>The seat at that table kept for the account, while it still is, or none.</summary>
    private (PlayedTable Table, TableSeat Seat)? InvitedSeat(string id, AccountId me) =>
        registry.ById(id) is { IsOver: false } table && table.Seats.FirstOrDefault(seat => seat.InvitedAccount == me && !seat.Taken) is { } seat
            ? (table, seat)
            : null;

    /// <summary>
    /// Invitations nobody answered in a day count as declined (ADR 0129): the seat is let go of the way a decline
    /// lets go of it, and the inviter's card says who did not answer. Under the claim gate, as every seat change is.
    /// </summary>
    private async Task ExpireInvitationsAsync()
    {
        var now = clock.GetUtcNow();
        foreach (var table in registry.All().Where(table => !table.IsOver))
        {
            foreach (var seat in table.Seats.Where(seat => seat.InvitedAccount is not null && !seat.Taken).ToArray())
            {
                // Ran out, or the two are no longer friends (removing a friend cancels the invitation between them).
                var inviter = table.Seats.FirstOrDefault(other => other != seat && other.Account is not null)?.Account;
                var unfriended = Friends is { } friends && inviter is { } host && !await friends.AreFriendsAsync(host, seat.InvitedAccount!.Value);
                if (now - (seat.InvitedAt ?? now) >= InvitationFor || unfriended)
                {
                    await LetGoAsync(table, seat, declinedBy: unfriended ? null : seat.InvitedAccount);
                }
            }
        }
    }

    /// <summary>
    /// The seat kept for a friend is kept for nobody now: fresh keys, so a link may name it again, and the table
    /// is its inviter's alone to decide on. Saved before it counts; nothing changed once the table is gone.
    /// </summary>
    private async Task<bool> LetGoAsync(PlayedTable table, TableSeat seat, AccountId? declinedBy)
    {
        var previous = registry.KeysOf(seat);
        var before = seat.DeclinedBy;
        if (!registry.Rekey(table, seat, SeatKeys.Offered()))
        {
            return false;
        }

        seat.DeclinedBy = declinedBy;
        try
        {
            await table.RecordAsync(registry.Codes);
            table.Api.Changes.Bump();
            return true;
        }
        catch
        {
            registry.Rekey(table, seat, previous);
            seat.DeclinedBy = before;
            throw;
        }
    }

    /// <summary>The invited friend takes the seat kept for them (ADR 0129): the claim of ADR 0116, by their account instead of a key.</summary>
    private async Task<StudioResponse> AcceptAsync(string id, SignedIn player)
    {
        var me = player.Account.Id;
        await Claims.WaitAsync();
        try
        {
            if (InvitedSeat(id, me) is not { } invited)
            {
                return Problem(404, "Lobby.NotInvited", "This invitation is no longer open: it was cancelled, it ran out, or the table is gone.");
            }

            var (table, seat) = invited;
            if (clock.GetUtcNow() - (seat.InvitedAt ?? clock.GetUtcNow()) >= InvitationFor)
            {
                await LetGoAsync(table, seat, declinedBy: me);
                return Problem(409, "Lobby.InvitationExpired", "This invitation ran out: it waited a day. Ask your friend for a new one.");
            }

            // Removing a friend cancels the invitation between the two, even on a card drawn before the removal.
            var inviterSeat = table.Seats.FirstOrDefault(other => other != seat && other.Account is not null);
            if (Friends is { } friends && inviterSeat?.Account is { } host && !await friends.AreFriendsAsync(host, me))
            {
                await LetGoAsync(table, seat, declinedBy: null);
                return Problem(404, "Lobby.NotInvited", "This invitation is no longer open: you are no longer friends.");
            }

            if (TooManyOngoing(me, AgainstBot(table)) is { } crowded)
            {
                return crowded;
            }

            if (!await TakeAsync(table, [seat], me))
            {
                return Problem(409, "Lobby.Gone", "This table is no longer available: it was closed.");
            }

            seat.DeclinedBy = null;
            table.Touch(clock.GetUtcNow());
            if (inviterSeat?.Account is { } inviter)
            {
                await NoticeAsync(inviter, $"{player.Account.Username.Value} accepted", "Take your seat: the battle begins when you are both there.", $"/play?table={Uri.EscapeDataString(table.Id)}", $"invite:{table.Id}");
            }

            return Json(new { table = await DescribeAsync(table), seats = new[] { new { seat = seat.Name, token = seat.Token } } });
        }
        finally
        {
            Claims.Release();
        }
    }

    private async Task<StudioResponse> DeclineAsync(string id, SignedIn player)
    {
        var me = player.Account.Id;
        await Claims.WaitAsync();
        try
        {
            if (InvitedSeat(id, me) is not { } invited)
            {
                return Problem(404, "Lobby.NotInvited", "This invitation is no longer open.");
            }

            var (table, seat) = invited;
            if (!await LetGoAsync(table, seat, declinedBy: me))
            {
                return Problem(409, "Lobby.Gone", "This table is no longer available: it was closed.");
            }

            if (table.Seats.FirstOrDefault(other => other != seat && other.Account is not null)?.Account is { } inviter)
            {
                await NoticeAsync(inviter, "Invitation declined", $"{player.Account.Username.Value} declined your invitation.", "/lobby", $"invite:{table.Id}");
            }

            return Json(new { declined = true });
        }
        finally
        {
            Claims.Release();
        }
    }

    /// <summary>The inviter's table with a seat still kept, or the refusal: not theirs, begun, or gone.</summary>
    private StudioResponse? InviterRefusal(string id, AccountId me, out PlayedTable table, out TableSeat seat)
    {
        table = null!;
        seat = null!;
        if (registry.ById(id) is not { IsOver: false } found)
        {
            return Problem(404, "Lobby.Gone", "This table is no longer available.");
        }

        if (!Inviter(found, me))
        {
            return Problem(403, "Lobby.NotYours", "This is not your table.");
        }

        if (found.Seats.FirstOrDefault(seat => seat.Person is not null && !seat.Taken) is not { } waiting)
        {
            return Problem(409, "Lobby.NoSeatWaiting", "Every seat at this table is taken.");
        }

        table = found;
        seat = waiting;
        return null;
    }

    private async Task<StudioResponse> CancelAsync(string id, SignedIn player)
    {
        await Claims.WaitAsync();
        try
        {
            if (InviterRefusal(id, player.Account.Id, out var table, out var seat) is { } refused)
            {
                return refused;
            }

            if (seat.InvitedAccount is null)
            {
                return Problem(409, "Lobby.NotInvited", "No invitation is waiting at this table.");
            }

            return await LetGoAsync(table, seat, declinedBy: null)
                ? Json(new { cancelled = true })
                : Problem(409, "Lobby.Gone", "This table is no longer available: it was closed.");
        }
        finally
        {
            Claims.Release();
        }
    }

    /// <summary>The seat is kept for another friend (ADR 0129): after a cancel, a decline, or an expiry.</summary>
    private async Task<StudioResponse> ReinviteAsync(string id, string body, SignedIn player)
    {
        var me = player.Account.Id;
        var friendId = JsonSerializer.Deserialize<Opening>(body, Web)?.Friend;
        await Claims.WaitAsync();
        try
        {
            if (InviterRefusal(id, me, out var table, out var seat) is { } refused)
            {
                return refused;
            }

            if (seat.InvitedAccount is not null)
            {
                return Problem(409, "Lobby.InvitationOutstanding", "This seat is already kept for a friend. Cancel that invitation first.");
            }

            if (await FriendRefusalAsync(me, friendId) is { } notFriend)
            {
                return notFriend;
            }

            var friend = AccountId.Parse(friendId!);
            var previous = registry.KeysOf(seat);
            var open = table.OpenSeats;
            var declined = seat.DeclinedBy;
            if (!registry.Rekey(table, seat, SeatKeys.KeptFor(friend, clock.GetUtcNow())))
            {
                return Problem(409, "Lobby.Gone", "This table is no longer available: it was closed.");
            }

            seat.DeclinedBy = null;
            table.OpenSeats = [.. open.Where(name => name != seat.Name)];
            try
            {
                await table.RecordAsync(registry.Codes);
            }
            catch
            {
                registry.Rekey(table, seat, previous);
                table.OpenSeats = open;
                seat.DeclinedBy = declined;
                throw;
            }

            table.Api.Changes.Bump();
            await InviteNoticeAsync(friend, me, table);
            return Json(new { invited = await InvitedOfAsync(table, seat) });
        }
        finally
        {
            Claims.Release();
        }
    }

    /// <summary>The seat kept for nobody is offered to everyone: the open place of ADR 0115.</summary>
    private async Task<StudioResponse> OpenToEveryoneAsync(string id, SignedIn player)
    {
        await Claims.WaitAsync();
        try
        {
            if (InviterRefusal(id, player.Account.Id, out var table, out var seat) is { } refused)
            {
                return refused;
            }

            return await OfferToEveryoneAsync(table, seat)
                ? Json(new { table = await DescribeAsync(table) })
                : Problem(409, "Lobby.Gone", "This table is no longer available: it was closed.");
        }
        finally
        {
            Claims.Release();
        }
    }

    /// <summary>Under the claim gate: the seat kept for a friend, or for nobody, becomes an open place anybody may take. Saved before it counts.</summary>
    private async Task<bool> OfferToEveryoneAsync(PlayedTable table, TableSeat seat)
    {
        var previous = registry.KeysOf(seat);
        var open = table.OpenSeats;
        var declined = seat.DeclinedBy;
        if (!registry.Rekey(table, seat, SeatKeys.Offered()))
        {
            return false;
        }

        seat.DeclinedBy = null;
        table.OpenSeats = [.. open.Append(seat.Name).Distinct(StringComparer.Ordinal)];
        try
        {
            await table.RecordAsync(registry.Codes);
        }
        catch
        {
            registry.Rekey(table, seat, previous);
            table.OpenSeats = open;
            seat.DeclinedBy = declined;
            throw;
        }

        table.Api.Changes.Bump();
        return true;
    }

    /// <summary>
    /// Closes a table that has not begun, for whoever holds every taken seat of it (ADR 0129): what a player does
    /// with a table nobody came to, which until now waited twenty hours for the operator.
    /// </summary>
    private async Task<StudioResponse> CloseAsync(string id, SignedIn player)
    {
        var me = player.Account.Id;
        await Claims.WaitAsync();
        try
        {
            if (registry.ById(id) is not { } table)
            {
                return Problem(404, "Lobby.Gone", "This table is no longer available.");
            }

            if (table.Session.HasBegun || table.IsOver)
            {
                return Problem(409, "Lobby.Begun", "This table has begun: leave it instead, so another player can take your team.");
            }

            var taken = table.Seats.Where(seat => seat.Person is not null && seat.Taken).ToArray();
            if (taken.Length == 0 || taken.Any(seat => seat.Account != me))
            {
                return Problem(403, "Lobby.NotYours", "Only the player holding every seat of a table can close it.");
            }

            registry.Remove(id);
            Console.WriteLine($"  Session {id} closed by its player before it began.");
            return new StudioResponse(204, StudioResponse.Plain, []);
        }
        finally
        {
            Claims.Release();
        }
    }

    private async Task InviteNoticeAsync(AccountId friend, AccountId me, PlayedTable table) =>
        await NoticeAsync(friend, "An invitation", $"{await NameAsync(me)} invites you to a {table.Session.Rules.Format} battle.", "/lobby#invitations", $"invite:{table.Id}");

    private async Task NoticeAsync(AccountId to, string title, string body, string url, string tag, string topic = "invitation")
    {
        if (Notices is { } notices)
        {
            await notices.SendAsync(to, AccountNotices.Payload(title, body, url, tag), topic);
        }
    }

    /// <summary>
    /// What a card says of the seat nobody has taken at the inviter's table: kept for a friend (who, since when),
    /// declined by one, or kept for nobody after a cancel, an unfriending or an expiry, which still needs a way
    /// out. Null for an open place and for a seat somebody holds.
    /// </summary>
    private async Task<object?> InvitedOfAsync(PlayedTable table, TableSeat seat)
    {
        if (seat.InvitedAccount is { } friend)
        {
            return new { id = friend.Value, username = await NameAsync(friend), sentAt = seat.InvitedAt, declined = false, free = false };
        }

        if (seat.DeclinedBy is { } decliner)
        {
            return new { id = decliner.Value, username = await NameAsync(decliner), sentAt = (DateTimeOffset?)null, declined = true, free = false };
        }

        if (seat.Person is not null && !seat.Taken && !table.OpenSeats.Contains(seat.Name) && !table.Session.HasBegun)
        {
            return new { id = (string?)null, username = (string?)null, sentAt = (DateTimeOffset?)null, declined = false, free = true };
        }

        return null;
    }

    private async Task<string> NameAsync(AccountId account) =>
        Accounts is { } accounts && await accounts.AccountAsync(account) is { } found ? found.Username.Value : "A player";

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

            if (TooManyOngoing(account, AgainstBot(table)) is { } crowded)
            {
                return crowded;
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
        var leaver = seat.Account;
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
            // The other player is told their opponent left (ADR 0129, stage 4), once the place is offered: a
            // match waiting on a team nobody holds is the thing to know about.
            if (table.Session.HasBegun)
            {
                foreach (var other in table.Seats.Where(other => other != seat && other.Account is { } held && held != leaver).Select(other => other.Account!.Value).Distinct())
                {
                    await NoticeAsync(other, $"{(leaver is { } who ? await NameAsync(who) : "A player")} left the table", "Their team is offered to another player. The match waits.", $"/play?table={Uri.EscapeDataString(table.Id)}", $"match:{table.Id}", "match");
                }
            }

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
                // A seat kept for the account is kept for nobody now, and a seat the account kept for a friend is
                // offered to everyone, as the account's own seat is about to be (ADR 0129): the friend was waiting on
                // a player who is gone.
                foreach (var kept in table.Seats.Where(seat => seat.InvitedAccount is not null && !seat.Taken && !table.IsOver).ToArray())
                {
                    if (kept.InvitedAccount == account)
                    {
                        await LetGoAsync(table, kept, declinedBy: null);
                    }
                    else if (Inviter(table, account))
                    {
                        await OfferToEveryoneAsync(table, kept);
                    }
                }

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
    private async Task<StudioResponse> InvitationsAsync(string body, AccountId? account)
    {
        var tokens = JsonSerializer.Deserialize<ResumeRequest>(body, Web)?.Tokens;
        if (tokens is null || tokens.Count is 0 or > 2)
        {
            return Problem(400, "Send the one or two keys the link carries.");
        }

        var invitations = await Task.WhenAll(tokens.Distinct(StringComparer.Ordinal).Select(async token =>
        {
            // A seat kept for a friend answers that friend alone (ADR 0129); to anybody else its key is spent.
            if (registry.ByInvitation(token) is { Table.IsOver: false } offered && (offered.Seat.InvitedAccount is null || offered.Seat.InvitedAccount == account))
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
            if (seats.Any(seat => seat.InvitedAccount is { } friend && friend != account))
            {
                return Problem(409, "Lobby.KeptForFriend", "This seat is kept for a friend of the player who opened the table.");
            }

            if (!table.Seats.Any(held => held.Account is not null && held.Account == account) && TooManyOngoing(account, AgainstBot(table)) is { } crowded)
            {
                return crowded;
            }

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

        foreach (var seat in seats)
        {
            seat.DeclinedBy = null;
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
            casual = table.Casual,
            waiting = !table.Session.HasBegun && !table.IsOver,
            over = table.IsOver,
            round = await AdminApi.RoundNowAsync(table),
            openSeats = OpenSeatCount(table),
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

        // Kept for a friend (ADR 0129): the card names who is expected, as it names who sits.
        if (!table.IsOver && !seat.Taken && seat.InvitedAccount is { } invited)
        {
            return new SeatSummary(seat.Name, "invited", await NameAsync(invited), turn);
        }

        // The account's id travels with a person's seat, so the other player's page can offer a rematch (ADR 0129).
        var name = seat.Account is { } id && Accounts is { } accounts ? (await accounts.AccountAsync(id))?.Username.Value : null;
        return new SeatSummary(seat.Name, "person", name ?? "Player", turn, seat.Account?.Value);
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
    /// <summary>Where a finished game is watched again: the battle view replaying it from the player's own seat.</summary>
    private static string ReplayLink(string table, string[] mine) =>
        $"/battle?replay={Uri.EscapeDataString(table)}&seat={(mine is ["player2", ..] ? "player2" : "player1")}";

    /// <summary>
    /// A finished game replayed for a player who sat at it (ADR 0156): the public events and final boards the admin
    /// panel's replay serves, and only to an account a kept result names at that table. Signed out is a 401 and
    /// somebody else's game a 403, so the page asks for the right sign-in rather than retrying.
    /// </summary>
    private async Task<StudioResponse> ReplayAsync(string id, AccountId? account)
    {
        if (account is not { } me)
        {
            return Problem(401, "Account.SignInRequired", "Sign in with the account that played this match to watch it again.");
        }

        if (Replay is not { } replay || (Results ?? composer.Results) is not { } results)
        {
            return Problem(404, "Lobby.NoReplay", "This host keeps no recording of finished games.");
        }

        var played = (await results.ListAsync()).Any(result => result.Table == id && result.Seats.Any(seat => seat.Account == me));
        return played ? await replay(id) : Problem(403, "Lobby.NotYourGame", "Only the players of this match can watch it again.");
    }

    private StudioResponse? SignInRequired(AccountId? account) =>
        Accounts is not null && account is null
            ? StudioResponse.OfJson(new { error = "Account.SignInRequired", message = "Sign in to play: open the home page and continue with Google." }, ArtifactJson.LineOptions, 401)
            : null;

    /// <summary>Whether a bot holds a seat at the table: the lobby lists and counts these apart (ADR 0147).</summary>
    internal static bool AgainstBot(PlayedTable table) => table.Seats.Any(seat => seat.Person is null);

    /// <summary>
    /// The refusal of a sixth game in progress of one kind (ADR 0147), checked under the claim gate so two requests
    /// cannot both take the fifth place; null where the account may go on, and always on a host without accounts.
    /// </summary>
    private StudioResponse? TooManyOngoing(AccountId? account, bool againstBot)
    {
        if (account is null)
        {
            return null;
        }

        var ongoing = registry.All().Count(table => !table.IsOver && AgainstBot(table) == againstBot && table.Seats.Any(seat => seat.Account == account));
        return ongoing < OngoingPerKind
            ? null
            : Problem(409, "Lobby.TooManyGames", againstBot
                ? $"You already have {OngoingPerKind} games against bots in progress. Finish or concede one to start another."
                : $"You already have {OngoingPerKind} games against players in progress. Finish one, or close one nobody has joined, to start another.");
    }

    /// <summary>
    /// One page of a signed-in player's finished games of one kind (ADR 0147), newest first, from the kept results:
    /// who played whom, how it ended, and how to see it again. A table the host still holds opens with the seats this
    /// account had there; any other opens its recorded session, when the result says which one it was.
    /// </summary>
    private async Task<StudioResponse> FinishedAsync(bool againstBots, int page, AccountId? account)
    {
        if (account is not { } me || (Results ?? composer.Results) is not { } results)
        {
            return Json(new { total = 0, pages = 1, page = 1, items = Array.Empty<object>() });
        }

        var shown = FinishedGames.Page(await results.ListAsync(), me, againstBots, page);
        var items = new List<object>();
        foreach (var result in shown.Items)
        {
            var mine = result.Seats.Where(seat => seat.Account == me).Select(seat => seat.Seat).ToArray();
            var other = result.Seats.FirstOrDefault(seat => seat.Account != me);
            var held = result.Table is { } id && registry.ById(id) is { IsOver: true } table ? table : null;
            items.Add(new
            {
                match = result.MatchId,
                table = result.Table,
                finishedAt = result.FinishedAt,
                format = result.Format,
                casual = result.Casual,
                rounds = result.Rounds,
                // Both seats on one device: there is no side to have won or lost.
                outcome = mine.Length > 1 ? "both" : result.Winner is null ? "draw" : mine.Contains(result.Winner) ? "won" : "lost",
                you = mine,
                opponent = other is null ? new { kind = "self", name = "Shared device" }
                    : other.Agent is { } spec ? new { kind = "bot", name = BotName(composer.Agents.FirstOrDefault(agent => string.Equals(agent.Value, spec, StringComparison.OrdinalIgnoreCase))?.Label ?? spec) }
                    : new { kind = "person", name = other.Account is { } opponent ? await NameAsync(opponent) : "Player" },
                seats = held?.Seats.Where(seat => seat.Account == me).Select(seat => new { seat = seat.Name, token = seat.Token }).ToArray(),
                // The battle view's whole-match recap from this player's side (ADR 0156), not the viewer's trace.
                replay = held is null && result.Table is { } recorded ? ReplayLink(recorded, mine) : null,
            });
        }

        return Json(new { total = shown.Total, pages = shown.Pages, page = shown.Page + 1, items });
    }

    /// <summary>
    /// The players a signed-in player met most recently, from the kept results: their name, the record between them,
    /// the format and rating of their last game, and where their friendship stands, which says how to challenge them
    /// again (ADR 0153): a friend is invited, anyone else is asked to be a friend first, and nothing is sent to a
    /// stranger. An account deleted since is nobody to challenge and is left out. <c>relation</c> is null where this
    /// host keeps no friends.
    /// </summary>
    private async Task<StudioResponse> OpponentsAsync(AccountId? account)
    {
        if (account is not { } me || (Results ?? composer.Results) is not { } results || Accounts is not { } accounts)
        {
            return Json(new { opponents = Array.Empty<object>() });
        }

        var links = Friends is { } friends ? await friends.ListAsync(me) : null;
        var opponents = new List<object>();
        foreach (var opponent in RecentOpponents.Of(await results.ListAsync(), me))
        {
            if (await accounts.AccountAsync(opponent.Account) is not { } found)
            {
                continue;
            }

            opponents.Add(new
            {
                id = opponent.Account.Value,
                name = found.Username.Value,
                lastPlayedAt = opponent.LastPlayed,
                games = opponent.Games,
                won = opponent.Won,
                lost = opponent.Lost,
                drawn = opponent.Drawn,
                format = opponent.LastFormat,
                casual = opponent.LastCasual,
                relation = links switch
                {
                    null => null,
                    _ when links.Has(opponent.Account) => "friend",
                    _ when links.Asked(opponent.Account) => "asked",
                    _ when links.AskedBy(opponent.Account) => "asking",
                    _ => "none",
                },
            });
        }

        return Json(new { opponents });
    }

    /// <summary>
    /// The tables a signed-in player has seats at, on any device (ADR 0122): what "Your games" lists, with the
    /// tokens this browser plays them by. Finished ones are kept for the hour a host keeps them, as on a browser;
    /// the Finished tab reads every finished game from the kept results instead (ADR 0147).
    /// </summary>
    private async Task<StudioResponse> MineAsync(AccountId? account)
    {
        if (account is null)
        {
            return Json(new { tables = Array.Empty<object>(), invitations = Array.Empty<object>() });
        }

        // Under the gate, as every seat change is: an invitation that ran out is let go of as this list is read.
        await Claims.WaitAsync();
        try
        {
            await ExpireInvitationsAsync();
        }
        finally
        {
            Claims.Release();
        }

        var tables = await Task.WhenAll(registry.All()
            .Where(table => table.Seats.Any(seat => seat.Account == account))
            .Select(async table => (object)new
            {
                table = await DescribeAsync(table),
                seats = table.Seats.Where(seat => seat.Account == account).Select(seat => new { seat = seat.Name, token = seat.Token }).ToArray(),
                invited = table.IsOver || !Inviter(table, account.Value) ? null : await InvitedOfAsync(table, table.Seats.FirstOrDefault(seat => seat.Person is not null && !seat.Taken) ?? table.Seats[1]),
            }));

        // The tables waiting for this account (ADR 0129): each with who asked, and since when.
        var invitations = await Task.WhenAll(registry.All()
            .Where(table => !table.IsOver && table.Seats.Any(seat => seat.InvitedAccount == account && !seat.Taken))
            .Select(async table =>
            {
                var seat = table.Seats.First(seat => seat.InvitedAccount == account);
                var from = table.Seats.FirstOrDefault(other => other != seat && other.Account is not null)?.Account;
                return (object)new
                {
                    table = await DescribeAsync(table),
                    from = from is { } inviter ? new { id = inviter.Value, username = await NameAsync(inviter) } : null,
                    sentAt = seat.InvitedAt,
                };
            }));
        return Json(new { tables, invitations, ongoingPerKind = OngoingPerKind });
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

    private static StudioResponse Problem(int status, string code, string message) => Json(new { error = code, message }, status);

    /// <param name="Ranked">False opens a game between two players unranked (ADR 0155); a bot or a shared device never is.</param>
    private sealed record Opening(string? Opponent, string? Format, string? Friend, bool? Ranked = null);

    /// <param name="Turn">Whether this seat's player is being asked something now: whose turn it is.</param>
    private sealed record SeatSummary(string Seat, string Kind, string Name, bool Turn, string? Account = null);

    private sealed record ResumeRequest(IReadOnlyList<string>? Tokens);

    private sealed record BrowserSubscription(string? Endpoint, BrowserKeys? Keys);

    private sealed record BrowserKeys(string? P256dh, string? Auth);
}
