using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A table opened for a friend (ADR 0129): its second seat is kept for that account, nobody else reaches it, the
/// friend accepts or declines from their lobby, the inviter cancels, re-invites, opens the seat to everyone or
/// closes the table, one invitation waits at a time, and a day is as long as one waits.
/// </summary>
public sealed class LobbyInvitationsTests : IDisposable
{
    private const string Key = "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A";
    private const string Auth = "AAECAwQFBgcICQoLDA0ODw";

    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lobby-invitations-{Guid.CreateVersion7():N}");
    private readonly MovableClock _clock = new(new DateTimeOffset(2026, 11, 4, 20, 0, 0, TimeSpan.Zero));
    private readonly FileAccountStore _store;
    private readonly AccountRegistry _accounts;
    private readonly Friends _friends;
    private readonly AccountDevices _devices;
    private readonly AccountNoticeLog _log;
    private readonly Sender _sender = new();
    private LobbyApi? _lobby;

    public LobbyInvitationsTests()
    {
        _registry = _hosted.Registry();
        _store = new FileAccountStore(_root);
        _accounts = new AccountRegistry(_store, _clock);
        _friends = new Friends(new FileFriendStore(_root), _store, _clock);
        _devices = new AccountDevices(new FileDeviceStore(_root), _clock);
        _log = new AccountNoticeLog(new FileNoticeStore(_root), _clock);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<LobbyApi> LobbyAsync()
    {
        if (_lobby is null)
        {
            var api = new AccountApi(_accounts, new AccountSessions(await _store.SessionKeyAsync(Cancel), _clock), AccountsMode.Development);
            _lobby = new LobbyApi(_registry, _hosted.Composer, _clock) { Accounts = api, Friends = _friends, Notifier = new AccountNotifier(_log, new AccountNotices(_sender, _devices)) };
        }

        return _lobby;
    }

    /// <summary>A registered player with a phone registered as their device.</summary>
    private async Task<SignedIn> PlayerAsync(string name)
    {
        var account = (await _accounts.RegisterAsync(new ExternalIdentity(ExternalIdentity.Development, name.ToLowerInvariant()), name, Cancel)).Value;
        await _devices.AddAsync(account.Id, new PushDevice($"https://fcm.googleapis.com/fcm/send/{name}", Key, Auth, default), Cancel);
        return new SignedIn(account, null);
    }

    private async Task BefriendAsync(SignedIn one, SignedIn other)
    {
        (await _friends.AskAsync(one.Account.Id, other.Account.Username.Value, Cancel)).IsSuccess.ShouldBeTrue();
        (await _friends.AcceptAsync(other.Account.Id, one.Account.Id, Cancel)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task An_invitation_that_ran_out_is_let_go_of_by_the_hourly_job_in_another_process_and_the_host_sees_it()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var table = await InvitedTableAsync(mark, bastien);
        _clock.Advance(LobbyApi.InvitationFor);

        // The job: a registry of its own over the same store and index, and a lobby over it.
        using var job = await _hosted.RestartedAsync();
        var tended = await TableTend.TendAsync(new(job, new LobbyApi(job, _hosted.Composer, _clock) { Friends = _friends }, _clock), _clock.GetUtcNow());

        tended.Expired.ShouldBe(1);
        var stored = (await _hosted.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull();
        stored.Seats.Single(seat => seat.Slot == "player2").InvitedAccount.ShouldBeNull();
        (await table.Run!.RewrittenAsync(Cancel)).ShouldNotBeNull("the host that held the table lets it go at its next look");
    }

    [Fact]
    public async Task On_a_host_the_job_tends_an_invitation_that_ran_out_is_not_listed_nor_in_the_way_and_is_left_to_the_job()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        (await LobbyAsync()).Tended = true;
        var table = await InvitedTableAsync(mark, bastien);
        _clock.Advance(LobbyApi.InvitationFor);

        var listed = (await MineAsync(bastien)).GetProperty("invitations").GetArrayLength();
        var another = await OpenForAsync(mark, bastien);

        listed.ShouldBe(0, "it ran out");
        another.Status.ShouldBe(201, "an invitation that ran out stands in nobody's way");
        table.Seats[1].InvitedAccount.ShouldBe(bastien.Account.Id, "letting it go is the hourly job's");
    }

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private async Task<StudioResponse> OpenForAsync(SignedIn inviter, SignedIn friend) =>
        await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/tables", $$"""{"opponent":"friend","friend":"{{friend.Account.Id.Value}}"}""", player: inviter);

    private async Task<PlayedTable> InvitedTableAsync(SignedIn inviter, SignedIn friend)
    {
        var opened = await OpenForAsync(inviter, friend);
        opened.Status.ShouldBe(201);
        return (await _registry.ByIdAsync(Read(opened).GetProperty("table").GetProperty("id").GetString()!, Cancel))!;
    }

    private async Task<JsonElement> MineAsync(SignedIn player) => Read(await (await LobbyAsync()).HandleAsync("GET", "/api/lobby/mine", "", player: player));

    private async Task<StudioResponse> ActAsync(string method, PlayedTable table, string suffix, SignedIn player, string body = "{}") =>
        await (await LobbyAsync()).HandleAsync(method, $"/api/lobby/tables/{table.Id}{suffix}", body, player: player);

    private string? LastNoticeBody(string to) =>
        _sender.Sent.LastOrDefault(sent => sent.Endpoint == $"https://fcm.googleapis.com/fcm/send/{to}") is { } sent ? JsonDocument.Parse(sent.Payload).RootElement.GetProperty("body").GetString() : null;

    [Fact]
    public async Task A_table_opened_for_a_friend_keeps_its_seat_for_them_and_tells_their_phone()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        var stranger = await PlayerAsync("third");
        await BefriendAsync(mark, bastien);
        var lobby = await LobbyAsync();

        var table = await InvitedTableAsync(mark, bastien);

        var seat = table.Seats[1];
        seat.InvitedAccount.ShouldBe(bastien.Account.Id);
        seat.Taken.ShouldBeFalse();
        seat.Key.ShouldBeNull("no code and no link name a seat kept for a friend");
        seat.Code.ShouldBeNull();
        var listed = Read(await lobby.HandleAsync("GET", "/api/lobby/page/all/1", "")).GetProperty("items")[0];
        listed.GetProperty("openSeats").GetInt32().ShouldBe(0);
        listed.GetProperty("seats")[1].GetProperty("kind").GetString().ShouldBe("invited");
        listed.GetProperty("seats")[1].GetProperty("name").GetString().ShouldBe("bastien");
        (await lobby.HandleAsync("POST", $"/api/lobby/tables/{table.Id}/join", "{}", player: stranger)).Status.ShouldBe(409);
        (await lobby.HandleAsync("POST", "/api/lobby/claim", JsonSerializer.Serialize(new { tokens = new[] { seat.Invitation } }), player: stranger)).Status.ShouldBe(409);
        Read(await lobby.HandleAsync("POST", "/api/lobby/invitations", JsonSerializer.Serialize(new { tokens = new[] { seat.Invitation } }), player: stranger)).GetProperty("invitations")[0].GetProperty("kind").GetString().ShouldBe("spent");
        (await table.Api.HandleAsync("GET", "/api/seat/player2", "", seat.Invitation, null, null)).Status.ShouldBe(403, "an invitation is not a token");
        LastNoticeBody("bastien").ShouldBe($"MarkF invites you to a {table.Session.Rules.Format} battle.");

        var theirs = await MineAsync(bastien);
        theirs.GetProperty("invitations")[0].GetProperty("from").GetProperty("username").GetString().ShouldBe("MarkF");
        theirs.GetProperty("invitations")[0].GetProperty("table").GetProperty("id").GetString().ShouldBe(table.Id);
        var mine = (await MineAsync(mark)).GetProperty("tables")[0].GetProperty("invited");
        mine.GetProperty("username").GetString().ShouldBe("bastien");
        mine.GetProperty("declined").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task An_invitation_s_notice_still_on_its_way_holds_no_other_claim_waiting()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var lobby = await LobbyAsync();
        _sender.Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var opening = OpenForAsync(mark, bastien);
        await _sender.Began.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        (await lobby.ExpireInvitationsNowAsync().WaitAsync(TimeSpan.FromSeconds(5), Cancel)).ShouldBe(0, "the claim gate was let go of before the notice");
        opening.IsCompleted.ShouldBeFalse("the request answers once its notice is given");
        _sender.Hold.SetResult();
        (await opening).Status.ShouldBe(201);
    }

    [Fact]
    public async Task A_link_s_table_is_described_to_a_visitor_who_is_not_signed_in_naming_the_player_who_opened_it()
    {
        var rook = await PlayerAsync("Rook");
        var lobby = await LobbyAsync();
        var opened = await lobby.HandleAsync("POST", "/api/lobby/tables", """{"opponent":"person"}""", player: rook);
        var table = (await _registry.ByIdAsync(Read(opened).GetProperty("table").GetProperty("id").GetString()!, Cancel))!;

        var answer = Read(await lobby.HandleAsync("POST", "/api/lobby/invitations", JsonSerializer.Serialize(new { tokens = new[] { table.Seats[1].Invitation } }), player: null)).GetProperty("invitations")[0];

        answer.GetProperty("kind").GetString().ShouldBe("invitation");
        answer.GetProperty("seat").GetString().ShouldBe("player2");
        var described = answer.GetProperty("table");
        described.GetProperty("format").GetString().ShouldBe(table.Session.Rules.Format.ToString());
        described.GetProperty("waiting").GetBoolean().ShouldBeTrue();
        var seats = described.GetProperty("seats");
        seats[0].GetProperty("seat").GetString().ShouldBe("player1");
        seats[0].GetProperty("kind").GetString().ShouldBe("person");
        seats[0].GetProperty("name").GetString().ShouldBe("Rook", "the signed-out door names who invites from this answer");
        seats[0].TryGetProperty("account", out _).ShouldBeFalse("whoever the link was forwarded to holds no seat, so no account id (#971)");
        seats[1].GetProperty("kind").GetString().ShouldBe("open");
    }

    [Fact]
    public async Task A_listed_table_names_its_player_to_a_visitor_and_to_a_signed_in_player_without_a_seat_there_but_gives_neither_an_account_id()
    {
        var rook = await PlayerAsync("Rook");
        var stranger = await PlayerAsync("third");
        var lobby = await LobbyAsync();
        (await lobby.HandleAsync("POST", "/api/lobby/tables", """{"opponent":"person"}""", player: rook)).Status.ShouldBe(201);

        var signedOut = Read(await lobby.HandleAsync("GET", "/api/lobby/page/all/1", "")).GetProperty("items")[0].GetProperty("seats")[0];
        var signedIn = Read(await lobby.HandleAsync("GET", "/api/lobby/page/all/1", "", player: stranger)).GetProperty("items")[0].GetProperty("seats")[0];

        signedOut.GetProperty("name").GetString().ShouldBe("Rook");
        signedOut.TryGetProperty("account", out _).ShouldBeFalse();
        signedIn.GetProperty("name").GetString().ShouldBe("Rook");
        signedIn.TryGetProperty("account", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task A_seated_player_reads_the_other_seat_s_account_for_a_rematch()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var table = await InvitedTableAsync(mark, bastien);
        (await ActAsync("POST", table, "/accept", bastien)).Status.ShouldBe(200);

        var seats = (await MineAsync(mark)).GetProperty("tables")[0].GetProperty("table").GetProperty("seats");

        seats[1].GetProperty("account").GetString().ShouldBe(bastien.Account.Id.Value);
    }

    [Fact]
    public async Task An_account_seated_at_one_table_and_only_invited_at_another_reads_an_account_id_at_the_first_alone()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        await InvitedTableAsync(mark, bastien);
        (await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/tables", """{"opponent":"person"}""", player: bastien)).Status.ShouldBe(201);

        var mine = await MineAsync(bastien);

        mine.GetProperty("tables")[0].GetProperty("table").GetProperty("seats")[0].GetProperty("account").GetString().ShouldBe(bastien.Account.Id.Value);
        var invitedTo = mine.GetProperty("invitations")[0].GetProperty("table").GetProperty("seats")[0];
        invitedTo.GetProperty("name").GetString().ShouldBe("MarkF");
        invitedTo.TryGetProperty("account", out _).ShouldBeFalse("an invitation is not a seat");
    }

    [Fact]
    public async Task The_ranked_badge_is_the_host_s_word_and_no_seat_on_the_public_list_carries_an_account_id()
    {
        var rook = await PlayerAsync("Rook");
        var casual = await PlayerAsync("Casual");
        var lobby = await LobbyAsync();
        var ranked = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", """{"opponent":"person"}""", player: rook)).GetProperty("table").GetProperty("id").GetString();
        var unranked = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", """{"opponent":"person","ranked":false}""", player: casual)).GetProperty("table").GetProperty("id").GetString();

        var items = Read(await lobby.HandleAsync("GET", "/api/lobby/page/all/1", "")).GetProperty("items").EnumerateArray().ToArray();

        items.Single(item => item.GetProperty("id").GetString() == ranked).GetProperty("ranked").GetBoolean().ShouldBeTrue();
        items.Single(item => item.GetProperty("id").GetString() == unranked).GetProperty("ranked").GetBoolean().ShouldBeFalse();
        items.SelectMany(item => item.GetProperty("seats").EnumerateArray()).Count(seat => seat.TryGetProperty("account", out _)).ShouldBe(0);
    }

    [Theory]
    [InlineData("""{"opponent":"bot"}""")]
    [InlineData("""{"opponent":"hotseat"}""")]
    [InlineData("""{"opponent":"person","ranked":false,"format":"1v1"}""")]
    public async Task A_game_against_a_bot_on_a_shared_device_or_opened_unranked_is_not_ranked(string opening)
    {
        var rook = await PlayerAsync("Rook");

        var opened = await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/tables", opening, player: rook);

        opened.Status.ShouldBe(201);
        Read(opened).GetProperty("table").GetProperty("ranked").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_seat_kept_for_a_friend_leaves_the_game_ranked_since_an_account_takes_it()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);

        var table = await InvitedTableAsync(mark, bastien);

        (await MineAsync(mark)).GetProperty("tables")[0].GetProperty("table").GetProperty("ranked").GetBoolean().ShouldBeTrue();
        (await MineAsync(bastien)).GetProperty("invitations")[0].GetProperty("table").GetProperty("id").GetString().ShouldBe(table.Id);
        (await MineAsync(bastien)).GetProperty("invitations")[0].GetProperty("table").GetProperty("ranked").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task A_deleted_account_s_seat_whose_notice_is_still_on_its_way_holds_no_other_claim_waiting()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var table = await InvitedTableAsync(mark, bastien);
        (await ActAsync("POST", table, "/accept", bastien)).Status.ShouldBe(200);
        foreach (var seat in table.Seats)
        {
            seat.Person!.Arrive();
        }

        table.Session.Begin();
        var lobby = await LobbyAsync();
        var sender = new Sender { Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        lobby.Notifier = new AccountNotifier(_log, new AccountNotices(sender, _devices));

        var releasing = lobby.ReleaseAsync(bastien.Account.Id);
        await sender.Began.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        (await lobby.ExpireInvitationsNowAsync().WaitAsync(TimeSpan.FromSeconds(5), Cancel)).ShouldBe(0, "the claim gate was let go of before the notice");
        releasing.IsCompleted.ShouldBeFalse("the release returns once its notice is given");
        sender.Hold.SetResult();
        (await releasing).ShouldBe(1);
        sender.Sent.ShouldHaveSingleItem().Endpoint.ShouldEndWith("/MarkF");
    }

    [Fact]
    public async Task Only_the_invited_friend_accepts_and_only_once_and_the_inviter_is_told()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        var stranger = await PlayerAsync("third");
        await BefriendAsync(mark, bastien);
        var table = await InvitedTableAsync(mark, bastien);

        (await ActAsync("POST", table, "/accept", stranger)).Status.ShouldBe(404);
        var accepted = await ActAsync("POST", table, "/accept", bastien);
        var again = await ActAsync("POST", table, "/accept", bastien);

        accepted.Status.ShouldBe(200);
        var token = Read(accepted).GetProperty("seats")[0].GetProperty("token").GetString();
        table.Seats[1].Token.ShouldBe(token);
        table.Seats[1].Account.ShouldBe(bastien.Account.Id);
        table.Seats[1].Taken.ShouldBeTrue();
        table.Seats[1].InvitedAccount.ShouldBeNull();
        again.Status.ShouldBe(404);
        LastNoticeBody("MarkF").ShouldBe("Take your seat: the battle begins when you are both there.");
        (await MineAsync(bastien)).GetProperty("invitations").GetArrayLength().ShouldBe(0);
        (await MineAsync(bastien)).GetProperty("tables").GetArrayLength().ShouldBe(1, "the table is theirs now");
    }

    [Fact]
    public async Task Only_a_friend_can_be_invited_and_one_invitation_waits_at_a_time()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        var third = await PlayerAsync("third");
        await BefriendAsync(mark, bastien);

        var notFriend = await OpenForAsync(mark, third);
        var first = await OpenForAsync(mark, bastien);
        var second = await OpenForAsync(mark, bastien);

        notFriend.Status.ShouldBe(403);
        Read(notFriend).GetProperty("error").GetString().ShouldBe("Friend.NotFriends");
        first.Status.ShouldBe(201);
        second.Status.ShouldBe(409);
        Read(second).GetProperty("error").GetString().ShouldBe("Lobby.InvitationOutstanding");
        _registry.HeldTables().Count.ShouldBe(1, "the refused table was never opened");
    }

    [Fact]
    public async Task Cancel_decline_and_expiry_each_leave_the_inviter_s_table_with_no_seat_kept()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var table = await InvitedTableAsync(mark, bastien);

        (await ActAsync("DELETE", table, "/invitation", bastien)).Status.ShouldBe(403, "only the inviter cancels");
        (await ActAsync("DELETE", table, "/invitation", mark)).Status.ShouldBe(200);
        table.Seats[1].InvitedAccount.ShouldBeNull();
        var cancelled = (await MineAsync(mark)).GetProperty("tables")[0].GetProperty("invited");
        cancelled.GetProperty("free").GetBoolean().ShouldBeTrue("the ways out stay on the card after a cancel");
        cancelled.GetProperty("declined").GetBoolean().ShouldBeFalse();
        (await MineAsync(bastien)).GetProperty("invitations").GetArrayLength().ShouldBe(0);
        (await ActAsync("DELETE", table, "/invitation", mark)).Status.ShouldBe(409, "nothing waits any more");

        (await ActAsync("POST", table, "/invitation", mark, $$"""{"friend":"{{bastien.Account.Id.Value}}"}""")).Status.ShouldBe(200);
        table.Seats[1].InvitedAccount.ShouldBe(bastien.Account.Id);
        (await ActAsync("POST", table, "/decline", bastien)).Status.ShouldBe(200);
        table.Seats[1].InvitedAccount.ShouldBeNull();
        var declined = (await MineAsync(mark)).GetProperty("tables")[0].GetProperty("invited");
        declined.GetProperty("declined").GetBoolean().ShouldBeTrue();
        declined.GetProperty("username").GetString().ShouldBe("bastien");
        LastNoticeBody("MarkF").ShouldBe("bastien declined your invitation.");

        (await ActAsync("POST", table, "/invitation", mark, $$"""{"friend":"{{bastien.Account.Id.Value}}"}""")).Status.ShouldBe(200);
        _clock.Advance(LobbyApi.InvitationFor);
        (await MineAsync(bastien)).GetProperty("invitations").GetArrayLength().ShouldBe(0, "ran out");
        table.Seats[1].InvitedAccount.ShouldBeNull();
        (await MineAsync(mark)).GetProperty("tables")[0].GetProperty("invited").GetProperty("declined").GetBoolean().ShouldBeTrue();
        (await OpenForAsync(mark, bastien)).Status.ShouldBe(201, "nothing outstanding stands in the way of a new table");
    }

    [Fact]
    public async Task Removing_the_friend_cancels_the_invitation_between_the_two()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var table = await InvitedTableAsync(mark, bastien);

        await _friends.UnlinkAsync(bastien.Account.Id, mark.Account.Id, Cancel);

        // Accepted off a card drawn before the removal, with no listing in between: refused all the same.
        var accepted = await ActAsync("POST", table, "/accept", bastien);

        accepted.Status.ShouldBe(404);
        table.Seats[1].InvitedAccount.ShouldBeNull();
        table.Seats[1].Taken.ShouldBeFalse();
        (await MineAsync(bastien)).GetProperty("invitations").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Two_tables_opened_for_friends_at_once_leave_one_invitation_waiting()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        var third = await PlayerAsync("third");
        await BefriendAsync(mark, bastien);
        await BefriendAsync(mark, third);

        var answers = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => OpenForAsync(mark, index % 2 == 0 ? bastien : third)));

        answers.Count(answer => answer.Status == 201).ShouldBe(1);
        answers.Count(answer => answer.Status == 409).ShouldBe(5);
        _registry.HeldTables().Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_seat_let_go_of_can_be_opened_to_everyone_or_the_table_closed_by_its_player()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        var third = await PlayerAsync("third");
        await BefriendAsync(mark, bastien);
        var lobby = await LobbyAsync();
        var table = await InvitedTableAsync(mark, bastien);
        await ActAsync("POST", table, "/decline", bastien);

        (await ActAsync("POST", table, "/open", third)).Status.ShouldBe(403);
        (await ActAsync("POST", table, "/open", mark)).Status.ShouldBe(200);
        table.OpenSeats.ShouldBe(["player2"]);
        table.Seats[1].DeclinedBy.ShouldBeNull();
        (await lobby.HandleAsync("POST", $"/api/lobby/tables/{table.Id}/join", "{}", player: third)).Status.ShouldBe(200, "anyone takes an open seat");

        var other = await InvitedTableAsync(mark, bastien);
        (await ActAsync("DELETE", other, "", third)).Status.ShouldBe(403);
        (await ActAsync("DELETE", other, "", mark)).Status.ShouldBe(204);
        (await _registry.ByIdAsync(other.Id, Cancel)).ShouldBeNull();
        (await MineAsync(bastien)).GetProperty("invitations").GetArrayLength().ShouldBe(0);
    }

    /// <summary>One switch a device (ADR 0202): a signed-in seat's subscription is its account's device too, and goes with the seat's DELETE.</summary>
    [Fact]
    public async Task A_signed_in_seat_s_subscription_joins_its_account_s_devices_and_leaves_them_with_the_seat()
    {
        var mark = await PlayerAsync("MarkF");
        var lobby = new LobbyApi(_registry, _hosted.Composer, _clock, new PushNotices(_sender, "BP8", _clock)) { Accounts = (await LobbyAsync()).Accounts, Devices = _devices };
        var opened = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", """{"opponent":"bot"}""", player: mark));
        var table = (await _registry.ByIdAsync(opened.GetProperty("table").GetProperty("id").GetString()!, Cancel))!;
        var token = table.Seats[0].Token;
        var browser = JsonSerializer.Serialize(new { endpoint = "https://fcm.googleapis.com/fcm/send/laptop", keys = new { p256dh = Key, auth = Auth } });

        (await lobby.HandleAsync("PUT", "/api/lobby/push", browser, token, mark)).Status.ShouldBe(200);
        var joined = (await _devices.ListAsync(mark.Account.Id, Cancel)).Select(device => device.Endpoint).ToList();
        (await lobby.HandleAsync("DELETE", "/api/lobby/push", "", token, mark)).Status.ShouldBe(200);

        joined.ShouldBe(["https://fcm.googleapis.com/fcm/send/MarkF", "https://fcm.googleapis.com/fcm/send/laptop"], ignoreOrder: true);
        (await _devices.ListAsync(mark.Account.Id, Cancel)).Select(device => device.Endpoint).ShouldBe(["https://fcm.googleapis.com/fcm/send/MarkF"]);
    }

    [Fact]
    public async Task A_signed_in_seat_s_new_subscription_takes_the_old_one_s_place_among_its_account_s_devices()
    {
        var mark = await PlayerAsync("MarkF");
        var lobby = new LobbyApi(_registry, _hosted.Composer, _clock, new PushNotices(_sender, "BP8", _clock)) { Accounts = (await LobbyAsync()).Accounts, Devices = _devices };
        var opened = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", """{"opponent":"bot"}""", player: mark));
        var token = (await _registry.ByIdAsync(opened.GetProperty("table").GetProperty("id").GetString()!, Cancel))!.Seats[0].Token;
        static string Browser(string endpoint) => JsonSerializer.Serialize(new { endpoint, keys = new { p256dh = Key, auth = Auth } });

        (await lobby.HandleAsync("PUT", "/api/lobby/push", Browser("https://fcm.googleapis.com/fcm/send/old"), token, mark)).Status.ShouldBe(200);
        (await lobby.HandleAsync("PUT", "/api/lobby/push", Browser("https://fcm.googleapis.com/fcm/send/new"), token, mark)).Status.ShouldBe(200);

        (await _devices.ListAsync(mark.Account.Id, Cancel)).Select(device => device.Endpoint).ShouldBe(["https://fcm.googleapis.com/fcm/send/MarkF", "https://fcm.googleapis.com/fcm/send/new"], ignoreOrder: true);
    }

    /// <summary>The account's sender heard the push service say the endpoint is gone: the seat's page is told 410 and subscribes again, rather than adding it back.</summary>
    [Fact]
    public async Task A_subscription_the_account_s_push_service_said_is_gone_is_refused_for_a_seat_too()
    {
        var mark = await PlayerAsync("MarkF");
        var door = new AccountNotifier(_log, new AccountNotices(_sender, _devices));
        var lobby = new LobbyApi(_registry, _hosted.Composer, _clock, new PushNotices(_sender, "BP8", _clock)) { Accounts = (await LobbyAsync()).Accounts, Devices = _devices, Notifier = door };
        var opened = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", """{"opponent":"bot"}""", player: mark));
        var token = (await _registry.ByIdAsync(opened.GetProperty("table").GetProperty("id").GetString()!, Cancel))!.Seats[0].Token;
        _sender.Gone.Add("https://fcm.googleapis.com/fcm/send/MarkF");
        await door.NotifyAsync(mark.Account.Id, new Notice(NoticeKind.Invitation, "An invitation", "bastien invites you.", "/lobby", "invite:t1"), Cancel);

        var again = await lobby.HandleAsync("PUT", "/api/lobby/push", JsonSerializer.Serialize(new { endpoint = "https://fcm.googleapis.com/fcm/send/MarkF", keys = new { p256dh = Key, auth = Auth } }), token, mark);

        again.Status.ShouldBe(410);
        Read(again).GetProperty("message").GetString().ShouldBe("This subscription is gone from its notification service. Tap \"Notify me when it's my turn\" again.");
        (await _devices.ListAsync(mark.Account.Id, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_table_that_has_begun_is_not_closed_from_the_lobby()
    {
        var mark = await PlayerAsync("MarkF");
        var opened = Read(await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/tables", """{"opponent":"bot"}""", player: mark));
        var table = (await _registry.ByIdAsync(opened.GetProperty("table").GetProperty("id").GetString()!, Cancel))!;
        table.Seats[0].Person!.Arrive();
        table.Session.Begin();

        var closed = await ActAsync("DELETE", table, "", mark);

        closed.Status.ShouldBe(409);
        Read(closed).GetProperty("error").GetString().ShouldBe("Lobby.Begun");
        (await _registry.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_player_who_leaves_a_begun_match_is_announced_to_the_other_seat_s_account()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var table = await InvitedTableAsync(mark, bastien);
        var accepted = Read(await ActAsync("POST", table, "/accept", bastien));
        var token = accepted.GetProperty("seats")[0].GetProperty("token").GetString();
        foreach (var seat in table.Seats)
        {
            seat.Person!.Arrive();
        }

        table.Session.Begin();
        _sender.Sent.Clear();

        var left = await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/leave", "{}", token, bastien);

        left.Status.ShouldBe(200);
        _sender.Sent.ShouldHaveSingleItem().Endpoint.ShouldBe("https://fcm.googleapis.com/fcm/send/MarkF");
        var notice = JsonDocument.Parse(_sender.Sent[0].Payload).RootElement;
        notice.GetProperty("title").GetString().ShouldBe("bastien left the table");
        notice.GetProperty("url").GetString().ShouldBe($"/play?table={table.Id}");
        _sender.Sent[0].Topic.ShouldBe("match");
    }

    [Fact]
    public async Task With_push_off_the_inviter_s_bell_keeps_the_seat_taken_and_the_other_leaving_but_not_what_waits()
    {
        (await LobbyAsync()).Notifier = new AccountNotifier(_log, push: null);
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var table = await InvitedTableAsync(mark, bastien);
        (await _log.ListAsync(bastien.Account.Id, Cancel)).ShouldBeEmpty("an invitation waits in the bell's own list");
        var accepted = Read(await ActAsync("POST", table, "/accept", bastien));
        var token = accepted.GetProperty("seats")[0].GetProperty("token").GetString();
        foreach (var seat in table.Seats)
        {
            seat.Person!.Arrive();
        }

        table.Session.Begin();

        (await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/leave", "{}", token, bastien)).Status.ShouldBe(200);

        var kept = await _log.ListAsync(mark.Account.Id, Cancel);
        kept.Select(entry => entry.Kind).ShouldBe([NoticeKind.Left, NoticeKind.SeatTaken]);
        kept[0].Title.ShouldBe("bastien left the table");
        kept[1].Body.ShouldBe("Take your seat: the battle begins when you are both there.");
        _sender.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_deleted_account_frees_the_seats_it_kept_for_friends_and_the_seats_kept_for_it()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var opened = await InvitedTableAsync(mark, bastien);
        var keptForMark = await InvitedTableAsync(bastien, mark);
        var lobby = await LobbyAsync();

        await lobby.ReleaseAsync(mark.Account.Id);

        opened.Seats[1].InvitedAccount.ShouldBeNull("the friend was waiting on a player who is gone");
        opened.OpenSeats.ShouldBe(["player2", "player1"], "both places are anybody's now, as ADR 0122 offers a deleted account's seats");
        (await MineAsync(bastien)).GetProperty("invitations").GetArrayLength().ShouldBe(0);
        (await _registry.ByIdAsync(keptForMark.Id, Cancel)).ShouldNotBeNull("Bastien's table stays, with its seat kept for nobody");
        keptForMark.Seats[1].InvitedAccount.ShouldBeNull();
        var bastiens = (await MineAsync(bastien)).GetProperty("tables").EnumerateArray().Single(entry => entry.GetProperty("table").GetProperty("id").GetString() == keptForMark.Id).GetProperty("invited");
        bastiens.GetProperty("free").GetBoolean().ShouldBeTrue("kept for nobody now, with the ways out on the card");
        bastiens.GetProperty("declined").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_host_starting_over_the_store_rebuilds_the_table_still_waiting_for_the_friend()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var table = await InvitedTableAsync(mark, bastien);

        using var restored = await _hosted.RestartedAsync();

        var rebuilt = (await restored.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        rebuilt.Seats[1].InvitedAccount.ShouldBe(bastien.Account.Id);
        rebuilt.Seats[1].InvitedAt.ShouldBe(table.Seats[1].InvitedAt);
        rebuilt.Seats[1].Key.ShouldBeNull();
        rebuilt.Seats[0].Account.ShouldBe(mark.Account.Id);
        restored.Dispose();
        await rebuilt.QuiesceAsync(Cancel);
    }

    [Fact]
    public async Task An_invitation_and_its_decline_tell_both_players_lobbies_and_the_public_lists_to_read_again()
    {
        var changes = new LobbyChanges();
        (await LobbyAsync()).Changes = changes;
        _hosted.Composer.Lobby = changes;
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);

        var table = await InvitedTableAsync(mark, bastien);
        var invited = (changes.VersionOf(mark.Account.Id), changes.VersionOf(bastien.Account.Id), changes.Version);
        (await ActAsync("POST", table, "/decline", bastien)).Status.ShouldBe(200);

        invited.Item1.ShouldBeGreaterThan(0);
        invited.Item2.ShouldBeGreaterThan(0, "the friend's bell shows the invitation without a poll");
        invited.Item3.ShouldBeGreaterThan(0);
        changes.VersionOf(mark.Account.Id).ShouldBeGreaterThan(invited.Item1, "the inviter's lobby shows the decline");
        changes.VersionOf(bastien.Account.Id).ShouldBeGreaterThan(invited.Item2);
    }

    [Fact]
    public async Task A_change_of_a_table_that_moves_nothing_a_lobby_shows_tells_no_lobby()
    {
        var changes = new LobbyChanges();
        (await LobbyAsync()).Changes = changes;
        _hosted.Composer.Lobby = changes;
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var table = await InvitedTableAsync(mark, bastien);
        var before = (changes.Version, changes.VersionOf(mark.Account.Id), changes.VersionOf(bastien.Account.Id));

        table.Api.Changes.Bump();

        (changes.Version, changes.VersionOf(mark.Account.Id), changes.VersionOf(bastien.Account.Id)).ShouldBe(before, "a step of a match reaches its seats, not every lobby");
    }

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class Sender : IPushSender
    {
        public List<(string Endpoint, string Payload, string Topic)> Sent { get; } = [];

        /// <summary>The endpoints a push service says are gone.</summary>
        public HashSet<string> Gone { get; } = [];

        /// <summary>Set, a send waits on it, once it says it began: what shows a notice in flight holds no gate.</summary>
        public TaskCompletionSource? Hold { get; set; }

        public TaskCompletionSource Began { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<PushSent> SendAsync(PushSubscription to, string payload, string topic, CancellationToken cancellationToken = default)
        {
            Sent.Add((to.Endpoint, payload, topic));
            Began.TrySetResult();
            if (Hold is { } hold)
            {
                await hold.Task;
            }

            return Gone.Contains(to.Endpoint) ? PushSent.Gone : PushSent.Taken;
        }
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
