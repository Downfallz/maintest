using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Cli.Tests.Table;

public sealed class AccountApiTests : IDisposable
{
    private const string GoodToken = "a-token-google-signed";
    private const string ClientId = "1234-test.apps.googleusercontent.com";
    private static readonly ExternalIdentity Mark = new(ExternalIdentity.Google, "109876543210987654321");

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"accounts-api-{Guid.CreateVersion7():N}");
    private readonly MovableClock _clock = new(new DateTimeOffset(2026, 11, 3, 20, 14, 0, TimeSpan.Zero));
    private readonly FileAccountStore _store;

    public AccountApiTests()
    {
        _store = new FileAccountStore(_root);
    }

    private async Task<AccountApi> ApiAsync(AccountsMode mode = AccountsMode.Google) =>
        new(
            new AccountRegistry(_store, _clock),
            new AccountSessions(await _store.SessionKeyAsync(TestContext.Current.CancellationToken), _clock),
            mode,
            mode == AccountsMode.Google ? new OneToken() : null,
            mode == AccountsMode.Google ? ClientId : null);

    private static AccountRequest Signed(string? session) => new(session, Secure: true);

    private static AccountRequest Nobody => new(null, Secure: true);

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private static string? Session(StudioResponse response)
    {
        var cookie = response.Headers?.SingleOrDefault(header => header.Key == "Set-Cookie").Value;
        return cookie?.Split(';')[0][(AccountSessions.Cookie.Length + 1)..];
    }

    private static async Task<string> TicketAsync(AccountApi api) =>
        Json(await api.HandleAsync("POST", "/api/account/google", $$"""{"credential":"{{GoodToken}}"}""", Nobody)).GetProperty("ticket").GetString()!;

    [Fact]
    public async Task A_sign_in_with_no_account_is_asked_for_a_name_and_given_no_session_yet()
    {
        var api = await ApiAsync();

        var answer = await api.HandleAsync("POST", "/api/account/google", $$"""{"credential":"{{GoodToken}}"}""", Nobody);

        answer.Status.ShouldBe(200);
        Json(answer).GetProperty("needsUsername").GetBoolean().ShouldBeTrue();
        Json(answer).GetProperty("ticket").GetString().ShouldNotBeNullOrEmpty();
        Session(answer).ShouldBeNull();
    }

    [Fact]
    public async Task Choosing_a_name_with_the_ticket_registers_and_signs_in()
    {
        var api = await ApiAsync();

        var answer = await api.HandleAsync("POST", "/api/account", $$"""{"ticket":"{{await TicketAsync(api)}}","username":"MarkF"}""", Nobody);

        answer.Status.ShouldBe(200);
        Json(answer).GetProperty("account").GetProperty("username").GetString().ShouldBe("MarkF");
        var me = Json(await api.HandleAsync("GET", "/api/account", "", Signed(Session(answer))));
        me.GetProperty("account").GetProperty("username").GetString().ShouldBe("MarkF");
    }

    [Fact]
    public async Task A_sign_in_that_has_an_account_is_signed_in_at_once()
    {
        var api = await ApiAsync();
        await new AccountRegistry(_store, _clock).RegisterAsync(Mark, "MarkF", TestContext.Current.CancellationToken);

        var answer = await api.HandleAsync("POST", "/api/account/google", $$"""{"credential":"{{GoodToken}}"}""", Nobody);

        Json(answer).GetProperty("account").GetProperty("username").GetString().ShouldBe("MarkF");
        Session(answer).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_token_Google_did_not_sign_is_refused()
    {
        var answer = await (await ApiAsync()).HandleAsync("POST", "/api/account/google", """{"credential":"forged"}""", Nobody);

        answer.Status.ShouldBe(401);
        Json(answer).GetProperty("error").GetString().ShouldBe("Account.SignInRejected");
    }

    [Fact]
    public async Task A_ticket_that_has_run_out_registers_nothing()
    {
        var api = await ApiAsync();
        var ticket = await TicketAsync(api);
        _clock.Advance(AccountSessions.TicketLifetime);

        var answer = await api.HandleAsync("POST", "/api/account", $$"""{"ticket":"{{ticket}}","username":"MarkF"}""", Nobody);

        answer.Status.ShouldBe(401);
        Json(answer).GetProperty("error").GetString().ShouldBe("Account.TicketExpired");
    }

    [Fact]
    public async Task A_name_somebody_holds_is_refused_as_taken_and_a_bad_one_as_invalid()
    {
        var api = await ApiAsync();
        await new AccountRegistry(_store, _clock).RegisterAsync(new ExternalIdentity(ExternalIdentity.Google, "another"), "MarkF", TestContext.Current.CancellationToken);
        var ticket = await TicketAsync(api);

        var taken = await api.HandleAsync("POST", "/api/account", $$"""{"ticket":"{{ticket}}","username":"markf"}""", Nobody);
        var invalid = await api.HandleAsync("POST", "/api/account", $$"""{"ticket":"{{ticket}}","username":"x"}""", Nobody);

        taken.Status.ShouldBe(409);
        Json(taken).GetProperty("error").GetString().ShouldBe("Account.UsernameTaken");
        invalid.Status.ShouldBe(400);
        Json(invalid).GetProperty("error").GetString().ShouldBe("Account.UsernameInvalid");
    }

    [Fact]
    public async Task A_name_is_checked_while_it_is_typed()
    {
        var api = await ApiAsync();
        await new AccountRegistry(_store, _clock).RegisterAsync(Mark, "MarkF", TestContext.Current.CancellationToken);

        Json(await api.HandleAsync("GET", "/api/account/names/bastien", "", Nobody)).GetProperty("available").GetBoolean().ShouldBeTrue();
        var taken = Json(await api.HandleAsync("GET", "/api/account/names/MARKF", "", Nobody));
        taken.GetProperty("available").GetBoolean().ShouldBeFalse();
        taken.GetProperty("error").GetString().ShouldBe("Account.UsernameTaken");
        Json(await api.HandleAsync("GET", "/api/account/names/greedy", "", Nobody)).GetProperty("error").GetString().ShouldBe("Account.UsernameReserved");
    }

    [Fact]
    public async Task Without_a_session_nobody_is_signed_in()
    {
        var me = Json(await (await ApiAsync()).HandleAsync("GET", "/api/account", "", Signed("not-a-session")));

        me.GetProperty("account").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Signing_out_clears_the_cookie()
    {
        var answer = await (await ApiAsync()).HandleAsync("POST", "/api/account/signout", "{}", Nobody);

        answer.Headers.ShouldNotBeNull().Single().Value.ShouldContain("Max-Age=0");
    }

    [Fact]
    public async Task Signing_out_everywhere_refuses_every_session_issued_before()
    {
        var api = await ApiAsync(AccountsMode.Development);
        var phone = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));
        var laptop = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));

        await api.HandleAsync("POST", "/api/account/signout", """{"everywhere":true}""", Signed(phone));

        (await api.WhoAsync(Signed(laptop), TestContext.Current.CancellationToken)).ShouldBeNull();
        (await api.WhoAsync(Signed(phone), TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task A_session_a_day_old_is_issued_again_when_it_is_used()
    {
        var api = await ApiAsync(AccountsMode.Development);
        var session = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));
        (await api.WhoAsync(Signed(session), TestContext.Current.CancellationToken)).ShouldNotBeNull().Renewed.ShouldBeNull();

        _clock.Advance(AccountSessions.RenewAfter);

        (await api.WhoAsync(Signed(session), TestContext.Current.CancellationToken)).ShouldNotBeNull().Renewed.ShouldNotBeNull().ShouldStartWith(AccountSessions.Cookie + "=");
    }

    [Fact]
    public async Task Deleting_an_account_needs_its_session_and_its_name_typed()
    {
        var api = await ApiAsync(AccountsMode.Development);
        var session = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));

        var nobody = await api.HandleAsync("DELETE", "/api/account", """{"username":"MarkF"}""", Nobody);
        var unconfirmed = await api.HandleAsync("DELETE", "/api/account", """{"username":"Mark"}""", Signed(session));

        nobody.Status.ShouldBe(401);
        unconfirmed.Status.ShouldBe(400);
        Json(unconfirmed).GetProperty("error").GetString().ShouldBe("Account.ConfirmDelete");
        (await api.WhoAsync(Signed(session), TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_deleted_account_signs_nobody_in_frees_its_name_and_releases_its_seats()
    {
        var api = await ApiAsync(AccountsMode.Development);
        var released = new List<SharedKernel.Identifiers.AccountId>();
        api.ReleaseSeats = account =>
        {
            released.Add(account);
            return Task.CompletedTask;
        };
        var signedIn = await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody);
        var session = Session(signedIn);

        var deleted = await api.HandleAsync("DELETE", "/api/account", """{"username":"markf"}""", Signed(session));

        deleted.Status.ShouldBe(200);
        deleted.Headers.ShouldNotBeNull().Single().Value.ShouldContain("Max-Age=0");
        released.Select(id => id.Value).ShouldBe([Json(signedIn).GetProperty("account").GetProperty("id").GetString()]);
        (await api.WhoAsync(Signed(session), TestContext.Current.CancellationToken)).ShouldBeNull();
        Json(await api.HandleAsync("GET", "/api/account/names/MarkF", "", Nobody)).GetProperty("available").GetBoolean().ShouldBeTrue();
    }

    private const string Subscription = """{"endpoint":"https://push.example/phone","keys":{"p256dh":"BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A","auth":"AAECAwQFBgcICQoLDA0ODw"}}""";

    /// <summary>An API whose host keeps devices and has push keys: the lobby's "Notify me on this device" reaches it.</summary>
    private async Task<(AccountApi Api, AccountDevices Devices, DeviceSender Sender)> NotifyingAsync()
    {
        var api = await ApiAsync(AccountsMode.Development);
        var devices = new AccountDevices(new FileDeviceStore(_root), _clock);
        var sender = new DeviceSender();
        api.Devices = devices;
        api.Notices = new AccountNotices(sender, devices);
        return (api, devices, sender);
    }

    private static async Task<SharedKernel.Identifiers.AccountId> IdOf(AccountApi api, string? session) =>
        (await api.WhoAsync(Signed(session), TestContext.Current.CancellationToken)).ShouldNotBeNull().Account.Id;

    [Fact]
    public async Task A_signed_in_browser_registers_itself_as_a_device_and_can_be_tested()
    {
        var (api, devices, sender) = await NotifyingAsync();
        var session = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));

        var registered = await api.HandleAsync("PUT", "/api/account/push", Subscription, Signed(session));
        var tested = await api.HandleAsync("POST", "/api/account/push/test", """{"endpoint":"https://push.example/phone"}""", Signed(session));

        registered.Status.ShouldBe(200);
        Json(registered).GetProperty("notices").GetBoolean().ShouldBeTrue();
        Json(registered).GetProperty("devices").GetInt32().ShouldBe(1);
        (await devices.ListAsync(await IdOf(api, session), TestContext.Current.CancellationToken)).ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example/phone");
        tested.Status.ShouldBe(200);
        Json(tested).GetProperty("sent").GetBoolean().ShouldBeTrue();
        sender.Sent.ShouldHaveSingleItem().ShouldBe("https://push.example/phone");
    }

    [Fact]
    public async Task A_device_needs_a_session_a_real_subscription_and_a_host_that_sends_notices()
    {
        var (api, _, _) = await NotifyingAsync();
        var session = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));
        var silent = await ApiAsync(AccountsMode.Development);

        (await api.HandleAsync("PUT", "/api/account/push", Subscription, Nobody)).Status.ShouldBe(401);
        (await api.HandleAsync("PUT", "/api/account/push", """{"endpoint":"http://push.example/plain"}""", Signed(session))).Status.ShouldBe(400);
        (await silent.HandleAsync("PUT", "/api/account/push", Subscription, Signed(session))).Status.ShouldBe(404);
        (await silent.HandleAsync("POST", "/api/account/push/test", "{}", Signed(session))).Status.ShouldBe(404);
    }

    [Fact]
    public async Task A_browser_that_asks_to_be_forgotten_is_and_the_account_s_other_devices_stay()
    {
        var (api, devices, _) = await NotifyingAsync();
        var session = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));
        await api.HandleAsync("PUT", "/api/account/push", Subscription, Signed(session));
        await api.HandleAsync("PUT", "/api/account/push", Subscription.Replace("/phone", "/laptop", StringComparison.Ordinal), Signed(session));

        var forgotten = await api.HandleAsync("DELETE", "/api/account/push", """{"endpoint":"https://push.example/phone"}""", Signed(session));

        forgotten.Status.ShouldBe(200);
        Json(forgotten).GetProperty("notices").GetBoolean().ShouldBeFalse();
        (await devices.ListAsync(await IdOf(api, session), TestContext.Current.CancellationToken)).ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example/laptop");
    }

    [Fact]
    public async Task A_subscription_its_push_service_let_go_of_is_answered_gone_until_the_browser_makes_a_new_one()
    {
        var (api, devices, sender) = await NotifyingAsync();
        var session = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));
        await api.HandleAsync("PUT", "/api/account/push", Subscription, Signed(session));
        sender.Gone = true;

        var test = await api.HandleAsync("POST", "/api/account/push/test", """{"endpoint":"https://push.example/phone"}""", Signed(session));
        var again = await api.HandleAsync("PUT", "/api/account/push", Subscription, Signed(session));
        var fresh = await api.HandleAsync("PUT", "/api/account/push", Subscription.Replace("/phone", "/phone-2", StringComparison.Ordinal), Signed(session));

        test.Status.ShouldBe(502);
        again.Status.ShouldBe(410);
        Json(again).GetProperty("error").GetString().ShouldBe("Account.DeviceGone");
        fresh.Status.ShouldBe(200);
        (await devices.ListAsync(await IdOf(api, session), TestContext.Current.CancellationToken)).ShouldHaveSingleItem().Endpoint.ShouldBe("https://push.example/phone-2");
    }

    [Fact]
    public async Task Signing_out_everywhere_and_deleting_the_account_forget_its_devices()
    {
        var (api, devices, _) = await NotifyingAsync();
        var session = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));
        var id = await IdOf(api, session);
        await api.HandleAsync("PUT", "/api/account/push", Subscription, Signed(session));

        await api.HandleAsync("POST", "/api/account/signout", """{"everywhere":true}""", Signed(session));
        (await devices.ListAsync(id, TestContext.Current.CancellationToken)).ShouldBeEmpty("signed out everywhere");

        session = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));
        await api.HandleAsync("PUT", "/api/account/push", Subscription, Signed(session));
        await api.HandleAsync("DELETE", "/api/account", """{"username":"MarkF"}""", Signed(session));
        (await devices.ListAsync(id, TestContext.Current.CancellationToken)).ShouldBeEmpty("deleted");
    }

    [Fact]
    public async Task An_account_that_saved_no_settings_reads_the_defaults_step_normal_speed_and_help()
    {
        var api = await ApiAsync(AccountsMode.Development);
        api.Preferences = new FilePreferenceStore(_root);
        var session = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));

        var read = Json(await api.HandleAsync("GET", "/api/account/preferences", "", Signed(session)));

        read.GetProperty("saved").GetBoolean().ShouldBeFalse();
        read.GetProperty("preferences").GetProperty("pace").GetString().ShouldBe("step");
        read.GetProperty("preferences").GetProperty("speed").GetDecimal().ShouldBe(1m);
        read.GetProperty("preferences").GetProperty("help").GetBoolean().ShouldBeTrue();
        read.GetProperty("preferences").GetProperty("reducedMotion").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Saved_settings_are_read_back_by_the_account_and_by_nobody_else()
    {
        var api = await ApiAsync(AccountsMode.Development);
        api.Preferences = new FilePreferenceStore(_root);
        var mark = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));
        var other = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"Other"}""", Nobody));

        var saved = await api.HandleAsync("PUT", "/api/account/preferences", """{"pace":"auto","speed":1.5,"help":false,"reducedMotion":true}""", Signed(mark));

        saved.Status.ShouldBe(200);
        var read = Json(await api.HandleAsync("GET", "/api/account/preferences", "", Signed(mark))).GetProperty("preferences");
        read.GetProperty("pace").GetString().ShouldBe("auto");
        read.GetProperty("speed").GetDecimal().ShouldBe(1.5m);
        read.GetProperty("help").GetBoolean().ShouldBeFalse();
        read.GetProperty("reducedMotion").GetBoolean().ShouldBeTrue();
        Json(await api.HandleAsync("GET", "/api/account/preferences", "", Signed(other))).GetProperty("saved").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Settings_need_a_session_a_store_and_values_the_match_settings_offer()
    {
        var api = await ApiAsync(AccountsMode.Development);
        var session = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));
        var storeless = await api.HandleAsync("GET", "/api/account/preferences", "", Signed(session));
        api.Preferences = new FilePreferenceStore(_root);

        var nobody = await api.HandleAsync("GET", "/api/account/preferences", "", Nobody);
        var badSpeed = await api.HandleAsync("PUT", "/api/account/preferences", """{"pace":"step","speed":3}""", Signed(session));
        var badPace = await api.HandleAsync("PUT", "/api/account/preferences", """{"pace":"fast","speed":1}""", Signed(session));

        storeless.Status.ShouldBe(404);
        nobody.Status.ShouldBe(401);
        badSpeed.Status.ShouldBe(400);
        Json(badPace).GetProperty("error").GetString().ShouldBe("Account.PreferencesInvalid");
    }

    [Fact]
    public async Task Deleting_the_account_forgets_its_settings()
    {
        var api = await ApiAsync(AccountsMode.Development);
        var store = new FilePreferenceStore(_root);
        api.Preferences = store;
        var session = Session(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));
        var id = await IdOf(api, session);
        await api.HandleAsync("PUT", "/api/account/preferences", """{"pace":"auto","speed":2,"help":true,"reducedMotion":false}""", Signed(session));

        await api.HandleAsync("DELETE", "/api/account", """{"username":"MarkF"}""", Signed(session));

        (await store.GetAsync(id, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task A_typed_name_signs_in_as_the_same_account_each_time()
    {
        var api = await ApiAsync(AccountsMode.Development);

        var first = Json(await api.HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody));
        var again = Json(await api.HandleAsync("POST", "/api/account/dev", """{"username":"markf"}""", Nobody));

        again.GetProperty("account").GetProperty("id").GetString().ShouldBe(first.GetProperty("account").GetProperty("id").GetString());
        again.GetProperty("account").GetProperty("username").GetString().ShouldBe("MarkF");
    }

    [Fact]
    public async Task Each_mode_answers_only_its_own_sign_in()
    {
        (await (await ApiAsync(AccountsMode.Google)).HandleAsync("POST", "/api/account/dev", """{"username":"MarkF"}""", Nobody)).Status.ShouldBe(404);
        (await (await ApiAsync(AccountsMode.Development)).HandleAsync("POST", "/api/account/google", """{"credential":"x"}""", Nobody)).Status.ShouldBe(404);
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_a_bad_request()
    {
        (await (await ApiAsync()).HandleAsync("POST", "/api/account/google", "{", Nobody)).Status.ShouldBe(400);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Google, as far as these tests need it: one token is Mark's, every other one is nobody's.</summary>
    private sealed class OneToken : IIdentityTokenVerifier
    {
        public Task<ExternalIdentity?> VerifyAsync(string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(token == GoodToken ? Mark : (ExternalIdentity?)null);
    }

    /// <summary>A push service as far as these tests need one: it takes every notice, or says every subscription is gone.</summary>
    private sealed class DeviceSender : IPushSender
    {
        public List<string> Sent { get; } = [];

        public bool Gone { get; set; }

        public Task<bool> SendAsync(PushSubscription to, string payload, string topic, CancellationToken cancellationToken = default)
        {
            if (Gone)
            {
                return Task.FromResult(false);
            }

            Sent.Add(to.Endpoint);
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
