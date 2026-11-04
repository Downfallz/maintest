using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The friends routes (ADR 0129) over a developer's host: asking by name, accepting, the silent ways out, each
/// signed in, and the two notices that reach the other player's devices.
/// </summary>
public sealed class FriendsApiTests : IDisposable
{
    private const string Subscription = """{"endpoint":"https://push.example/{0}","keys":{"p256dh":"BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A","auth":"AAECAwQFBgcICQoLDA0ODw"}}""";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"friends-api-{Guid.CreateVersion7():N}");
    private readonly Sender _sender = new();
    private readonly AccountApi _accounts;
    private readonly FriendsApi _api;
    private readonly Friends _friends;

    public FriendsApiTests()
    {
        var clock = TimeProvider.System;
        var store = new FileAccountStore(_root);
        var sessions = new AccountSessions(store.SessionKeyAsync().GetAwaiter().GetResult(), clock);
        _accounts = new AccountApi(new AccountRegistry(store, clock), sessions, AccountsMode.Development);
        var devices = new AccountDevices(new FileDeviceStore(_root), clock);
        _accounts.Devices = devices;
        _accounts.Notices = new AccountNotices(_sender, devices);
        _friends = new Friends(new FileFriendStore(_root), store, clock);
        _accounts.ForgetFriends = id => _friends.ForgetAsync(id);
        _api = new FriendsApi(_friends, _accounts, _accounts.Notices);
    }

    private static AccountRequest Signed(string? session) => new(session, Secure: true);

    private static AccountRequest Nobody => new(null, Secure: true);

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    /// <summary>Signs the name in, with a phone registered as its device, and answers the session.</summary>
    private async Task<string> SignInAsync(string name)
    {
        var signedIn = await _accounts.HandleAsync("POST", "/api/account/dev", $$"""{"username":"{{name}}"}""", Nobody);
        var session = signedIn.Headers!.Single(header => header.Key == "Set-Cookie").Value.Split(';')[0][(AccountSessions.Cookie.Length + 1)..];
        (await _accounts.HandleAsync("PUT", "/api/account/push", Subscription.Replace("{0}", name, StringComparison.Ordinal), Signed(session))).Status.ShouldBe(200);
        return session;
    }

    private Task<StudioResponse> Ask(string session, string name) => _api.HandleAsync("POST", "/api/friends/requests", $$"""{"username":"{{name}}"}""", Signed(session));

    private async Task<JsonElement> List(string session) => Json(await _api.HandleAsync("GET", "/api/friends", "", Signed(session)));

    private static string IdOf(JsonElement list, string section, int index = 0) => list.GetProperty(section)[index].GetProperty("id").GetString()!;

    [Fact]
    public async Task Asking_by_name_lists_the_request_on_both_sides_by_name_and_tells_the_other_s_phone()
    {
        var mark = await SignInAsync("MarkF");
        var bastien = await SignInAsync("bastien");

        var asked = await Ask(mark, "Bastien");

        asked.Status.ShouldBe(200);
        Json(asked).GetProperty("accepted").GetBoolean().ShouldBeFalse();
        Json(asked).GetProperty("other").GetProperty("username").GetString().ShouldBe("bastien");
        (await List(mark)).GetProperty("outgoing")[0].GetProperty("username").GetString().ShouldBe("bastien");
        (await List(bastien)).GetProperty("incoming")[0].GetProperty("username").GetString().ShouldBe("MarkF");
        var notice = _sender.Sent.ShouldHaveSingleItem();
        notice.Endpoint.ShouldBe("https://push.example/bastien");
        notice.Topic.ShouldBe("friend");
        var payload = JsonDocument.Parse(notice.Payload).RootElement;
        payload.GetProperty("body").GetString().ShouldBe("MarkF wants to be your friend.");
        payload.GetProperty("url").GetString().ShouldBe("/lobby#friends");
    }

    [Fact]
    public async Task Accepting_makes_friends_and_tells_the_asker_s_phone()
    {
        var mark = await SignInAsync("MarkF");
        var bastien = await SignInAsync("bastien");
        await Ask(mark, "bastien");
        var request = IdOf(await List(bastien), "incoming");

        var accepted = await _api.HandleAsync("POST", $"/api/friends/requests/{request}/accept", "", Signed(bastien));

        accepted.Status.ShouldBe(200);
        (await List(mark)).GetProperty("friends")[0].GetProperty("username").GetString().ShouldBe("bastien");
        (await List(bastien)).GetProperty("friends")[0].GetProperty("username").GetString().ShouldBe("MarkF");
        _sender.Sent.Count.ShouldBe(2);
        _sender.Sent[1].Endpoint.ShouldBe("https://push.example/MarkF");
        JsonDocument.Parse(_sender.Sent[1].Payload).RootElement.GetProperty("body").GetString().ShouldBe("bastien accepted your friend request.");
    }

    [Fact]
    public async Task Asking_back_accepts_and_says_so()
    {
        var mark = await SignInAsync("MarkF");
        var bastien = await SignInAsync("bastien");
        await Ask(mark, "bastien");

        var asked = await Ask(bastien, "markf");

        Json(asked).GetProperty("accepted").GetBoolean().ShouldBeTrue();
        JsonDocument.Parse(_sender.Sent[^1].Payload).RootElement.GetProperty("body").GetString().ShouldBe("bastien accepted your friend request.");
    }

    [Fact]
    public async Task Declining_withdrawing_and_removing_are_silent_and_leave_nothing()
    {
        var mark = await SignInAsync("MarkF");
        var bastien = await SignInAsync("bastien");
        await Ask(mark, "bastien");
        var markId = IdOf(await List(bastien), "incoming");
        var bastienId = IdOf(await List(mark), "outgoing");
        _sender.Sent.Clear();

        (await _api.HandleAsync("DELETE", $"/api/friends/requests/{markId}", "", Signed(bastien))).Status.ShouldBe(200);
        (await List(mark)).GetProperty("outgoing").GetArrayLength().ShouldBe(0, "declined: the asker's request is gone");

        await Ask(mark, "bastien");
        (await _api.HandleAsync("DELETE", $"/api/friends/requests/{bastienId}", "", Signed(mark))).Status.ShouldBe(200);
        (await List(bastien)).GetProperty("incoming").GetArrayLength().ShouldBe(0, "withdrawn");

        await Ask(mark, "bastien");
        await _api.HandleAsync("POST", $"/api/friends/requests/{markId}/accept", "", Signed(bastien));
        (await _api.HandleAsync("DELETE", $"/api/friends/{bastienId}", "", Signed(mark))).Status.ShouldBe(200);
        (await List(bastien)).GetProperty("friends").GetArrayLength().ShouldBe(0, "removed");
        _sender.Sent.Select(sent => JsonDocument.Parse(sent.Payload).RootElement.GetProperty("body").GetString()).ShouldAllBe(body => body!.EndsWith("friend.", StringComparison.Ordinal) || body.EndsWith("request.", StringComparison.Ordinal));
        _sender.Sent.Count.ShouldBe(3, "two requests and one acceptance; nothing for a decline, a withdrawal or a removal");
    }

    [Fact]
    public async Task The_refusals_say_why_and_a_stranger_sees_nothing()
    {
        var mark = await SignInAsync("MarkF");
        await SignInAsync("bastien");

        (await _api.HandleAsync("GET", "/api/friends", "", Nobody)).Status.ShouldBe(401);
        Json(await Ask(mark, "nobody-here")).GetProperty("error").GetString().ShouldBe("Friend.NoSuchPlayer");
        (await Ask(mark, "nobody-here")).Status.ShouldBe(404);
        (await Ask(mark, "MarkF")).Status.ShouldBe(400);
        await Ask(mark, "bastien");
        (await Ask(mark, "bastien")).Status.ShouldBe(409);
        Json(await Ask(mark, "bastien")).GetProperty("error").GetString().ShouldBe("Friend.AlreadyAsked");
        (await _api.HandleAsync("POST", "/api/friends/requests/acct_nobody/accept", "", Signed(mark))).Status.ShouldBe(404);
        (await _api.HandleAsync("POST", "/api/friends/elsewhere", "", Signed(mark))).Status.ShouldBe(404);
    }

    [Fact]
    public async Task A_deleted_account_is_on_nobody_s_list_and_is_named_Player_nowhere()
    {
        var mark = await SignInAsync("MarkF");
        var bastien = await SignInAsync("bastien");
        await Ask(mark, "bastien");
        await _api.HandleAsync("POST", $"/api/friends/requests/{IdOf(await List(bastien), "incoming")}/accept", "", Signed(bastien));

        (await _accounts.HandleAsync("DELETE", "/api/account", """{"username":"MarkF"}""", Signed(mark))).Status.ShouldBe(200);

        (await List(bastien)).GetProperty("friends").GetArrayLength().ShouldBe(0);
    }

    public void Dispose()
    {
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
}
