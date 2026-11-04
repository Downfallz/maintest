using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.Learning;
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
    private readonly TableRegistry _registry = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lobby-invitations-{Guid.CreateVersion7():N}");
    private readonly MovableClock _clock = new(new DateTimeOffset(2026, 11, 4, 20, 0, 0, TimeSpan.Zero));
    private readonly FileAccountStore _store;
    private readonly AccountRegistry _accounts;
    private readonly Friends _friends;
    private readonly AccountDevices _devices;
    private readonly Sender _sender = new();
    private LobbyApi? _lobby;

    public LobbyInvitationsTests()
    {
        _store = new FileAccountStore(_root);
        _accounts = new AccountRegistry(_store, _clock);
        _friends = new Friends(new FileFriendStore(_root), _store, _clock);
        _devices = new AccountDevices(new FileDeviceStore(_root), _clock);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<LobbyApi> LobbyAsync()
    {
        if (_lobby is null)
        {
            var api = new AccountApi(_accounts, new AccountSessions(await _store.SessionKeyAsync(Cancel), _clock), AccountsMode.Development);
            _lobby = new LobbyApi(_registry, _hosted.Composer, _clock) { Accounts = api, Friends = _friends, Notices = new AccountNotices(_sender, _devices) };
        }

        return _lobby;
    }

    /// <summary>A registered player with a phone registered as their device.</summary>
    private async Task<SignedIn> PlayerAsync(string name)
    {
        var account = (await _accounts.RegisterAsync(new ExternalIdentity(ExternalIdentity.Development, name.ToLowerInvariant()), name, Cancel)).Value;
        await _devices.AddAsync(account.Id, new PushDevice($"https://push.example/{name}", Key, Auth, default), Cancel);
        return new SignedIn(account, null);
    }

    private async Task BefriendAsync(SignedIn one, SignedIn other)
    {
        (await _friends.AskAsync(one.Account.Id, other.Account.Username.Value, Cancel)).IsSuccess.ShouldBeTrue();
        (await _friends.AcceptAsync(other.Account.Id, one.Account.Id, Cancel)).IsSuccess.ShouldBeTrue();
    }

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private async Task<StudioResponse> OpenForAsync(SignedIn inviter, SignedIn friend) =>
        await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/tables", $$"""{"opponent":"friend","friend":"{{friend.Account.Id.Value}}"}""", player: inviter);

    private async Task<PlayedTable> InvitedTableAsync(SignedIn inviter, SignedIn friend)
    {
        var opened = await OpenForAsync(inviter, friend);
        opened.Status.ShouldBe(201);
        return _registry.ById(Read(opened).GetProperty("table").GetProperty("id").GetString()!)!;
    }

    private async Task<JsonElement> MineAsync(SignedIn player) => Read(await (await LobbyAsync()).HandleAsync("GET", "/api/lobby/mine", "", player: player));

    private async Task<StudioResponse> ActAsync(string method, PlayedTable table, string suffix, SignedIn player, string body = "{}") =>
        await (await LobbyAsync()).HandleAsync(method, $"/api/lobby/tables/{table.Id}{suffix}", body, player: player);

    private string? LastNoticeBody(string to) =>
        _sender.Sent.LastOrDefault(sent => sent.Endpoint == $"https://push.example/{to}") is { } sent ? JsonDocument.Parse(sent.Payload).RootElement.GetProperty("body").GetString() : null;

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
        _registry.Codes.Of(seat).ShouldBeNull();
        var listed = Read(await lobby.HandleAsync("GET", "/api/lobby", "")).GetProperty("tables")[0];
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
        _registry.All().Count.ShouldBe(1, "the refused table was never opened");
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
        _registry.All().Count.ShouldBe(1);
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
        _registry.ById(other.Id).ShouldBeNull();
        (await MineAsync(bastien)).GetProperty("invitations").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_table_that_has_begun_is_not_closed_from_the_lobby()
    {
        var mark = await PlayerAsync("MarkF");
        var opened = Read(await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/tables", """{"opponent":"bot"}""", player: mark));
        var table = _registry.ById(opened.GetProperty("table").GetProperty("id").GetString()!)!;
        table.Seats[0].Person!.Arrive();
        table.Session.Begin();

        var closed = await ActAsync("DELETE", table, "", mark);

        closed.Status.ShouldBe(409);
        Read(closed).GetProperty("error").GetString().ShouldBe("Lobby.Begun");
        _registry.ById(table.Id).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_host_starting_over_the_store_rebuilds_the_table_still_waiting_for_the_friend()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await BefriendAsync(mark, bastien);
        var table = await InvitedTableAsync(mark, bastien);

        using var after = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory));
        using var restored = new TableRegistry();
        await TableRestorer.RestoreAsync(after.Stored!, after.Composer, restored, _clock, Cancel);

        var rebuilt = restored.ById(table.Id).ShouldNotBeNull();
        rebuilt.Seats[1].InvitedAccount.ShouldBe(bastien.Account.Id);
        rebuilt.Seats[1].InvitedAt.ShouldBe(table.Seats[1].InvitedAt);
        rebuilt.Seats[1].Key.ShouldBeNull();
        rebuilt.Seats[0].Account.ShouldBe(mark.Account.Id);
        restored.Dispose();
        await rebuilt.QuiesceAsync(Cancel);
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

        public Task<bool> SendAsync(PushSubscription to, string payload, string topic, CancellationToken cancellationToken = default)
        {
            Sent.Add((to.Endpoint, payload, topic));
            return Task.FromResult(true);
        }
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
