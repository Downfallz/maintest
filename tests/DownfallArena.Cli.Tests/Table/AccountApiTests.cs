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

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
