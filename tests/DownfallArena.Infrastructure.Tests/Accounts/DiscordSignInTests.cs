using System.Net;
using System.Text;
using DownfallArena.Application.Accounts;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class DiscordSignInTests : IDisposable
{
    private const string ClientId = "1311111111111111111";
    private const string Code = "kR5tXbq0cWZgQe1uJ8vYp3nLmA7sD2";
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk.~abc";
    private const string Redirect = "https://play.downfallinstinct.com/signin/discord";
    private const string UserId = "80351110224678912";

    private readonly FakeDiscord _discord = new();
    private readonly HttpClient _http;

    public DiscordSignInTests() => _http = new HttpClient(_discord);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private DiscordSignIn Discord() => new(ClientId, _http);

    private static SignInProof Proof(string code = Code, string verifier = Verifier, string redirect = Redirect) =>
        SignInProof.Of((DiscordSignIn.CodeField, code), (DiscordSignIn.VerifierField, verifier), (DiscordSignIn.RedirectField, redirect));

    public void Dispose() => _http.Dispose();

    [Fact]
    public async Task A_code_Discord_issued_to_this_app_names_the_discord_user()
    {
        var discord = Discord();

        var identity = await discord.SignInAsync(Proof(), Cancel);

        identity.ShouldBe(new ExternalIdentity("discord", UserId));
        discord.Name.ShouldBe("discord");
        discord.ClientId.ShouldBe(ClientId);
    }

    [Fact]
    public async Task The_exchange_sends_the_code_the_verifier_the_redirect_and_the_client_id_and_no_secret()
    {
        await Discord().SignInAsync(Proof(), Cancel);

        var exchange = _discord.Exchanges.ShouldHaveSingleItem();
        exchange.ShouldBe(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = Code,
            ["code_verifier"] = Verifier,
            ["redirect_uri"] = Redirect,
            ["client_id"] = ClientId,
        });
        _discord.Bearers.ShouldBe(["access-1"]);
    }

    [Fact]
    public async Task A_code_used_once_already_is_refused()
    {
        var discord = Discord();
        (await discord.SignInAsync(Proof(), Cancel)).ShouldNotBeNull();

        (await discord.SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_code_whose_verifier_does_not_match_is_refused()
    {
        _discord.Verifier = "another-verifier-another-verifier-another-verifier";

        (await Discord().SignInAsync(Proof(), Cancel)).ShouldBeNull();
        _discord.Bearers.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_token_Discord_issued_to_another_app_is_refused()
    {
        _discord.Application = "999999999999999999";

        (await Discord().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_that_does_not_name_a_user_is_refused()
    {
        _discord.User = null;

        (await Discord().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12 34")]
    [InlineData("")]
    public async Task A_user_id_that_is_not_a_snowflake_is_refused(string id)
    {
        _discord.User = id;

        (await Discord().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task An_exchange_Discord_does_not_answer_with_a_token_is_a_refusal(HttpStatusCode status)
    {
        _discord.ExchangeStatus = status;

        (await Discord().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task An_answer_that_is_not_json_is_a_refusal()
    {
        _discord.Garbled = true;

        (await Discord().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task Discord_out_of_reach_is_a_refusal_not_an_exception()
    {
        _discord.Unreachable = true;

        (await Discord().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Theory]
    [InlineData("", Verifier, Redirect)]
    [InlineData("a code", Verifier, Redirect)]
    [InlineData(Code, "", Redirect)]
    [InlineData(Code, "too-short", Redirect)]
    [InlineData(Code, "dBjftJeZ4CVP+mB92K27uhbUJU1p1r/wW1gFWFOEjXk=abc", Redirect)]
    [InlineData(Code, Verifier, "")]
    [InlineData(Code, Verifier, "/signin/discord")]
    [InlineData(Code, Verifier, "http://play.downfallinstinct.com/signin/discord")]
    [InlineData(Code, Verifier, "https://play.downfallinstinct.com/lobby")]
    [InlineData(Code, Verifier, "https://play.downfallinstinct.com/signin/discord?next=/play")]
    [InlineData(Code, Verifier, "https://play.downfallinstinct.com/signin/discord#x")]
    [InlineData(Code, Verifier, "https://user@play.downfallinstinct.com/signin/discord")]
    [InlineData(Code, Verifier, "javascript:alert(1)")]
    public async Task A_proof_of_the_wrong_shape_is_refused_without_asking_discord(string code, string verifier, string redirect)
    {
        (await Discord().SignInAsync(Proof(code, verifier, redirect), Cancel)).ShouldBeNull();

        _discord.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task A_proof_missing_its_fields_is_refused_without_asking_discord()
    {
        (await Discord().SignInAsync(SignInProof.Of(), Cancel)).ShouldBeNull();

        _discord.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task A_laptops_table_returns_to_its_loopback_address_over_http()
    {
        (await Discord().SignInAsync(Proof(redirect: "http://localhost:5099/signin/discord"), Cancel)).ShouldNotBeNull();
        (await Discord().SignInAsync(Proof(code: "second", redirect: "http://127.0.0.1:5099/signin/discord"), Cancel)).ShouldNotBeNull();
    }

    [Fact]
    public void A_client_id_is_required()
    {
        Should.Throw<ArgumentException>(() => new DiscordSignIn(" ", _http));
    }

    /// <summary>
    /// Discord's two endpoints as the provider calls them: the token exchange, which takes a code once and only with the
    /// verifier its challenge was made from, and the current authorization, which names the app and the user.
    /// </summary>
    private sealed class FakeDiscord : HttpMessageHandler
    {
        private readonly HashSet<string> _used = [];

        public string Verifier { get; set; } = DiscordSignInTests.Verifier;

        public string Application { get; set; } = ClientId;

        public string? User { get; set; } = UserId;

        public HttpStatusCode ExchangeStatus { get; set; } = HttpStatusCode.OK;

        public bool Garbled { get; set; }

        public bool Unreachable { get; set; }

        public int Calls { get; private set; }

        public List<Dictionary<string, string>> Exchanges { get; } = [];

        public List<string> Bearers { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Unreachable)
            {
                throw new HttpRequestException("No route to discord.com.");
            }

            return (request.Method.Method, request.RequestUri!.AbsoluteUri) switch
            {
                ("POST", "https://discord.com/api/v10/oauth2/token") => await ExchangeAsync(request, cancellationToken),
                ("GET", "https://discord.com/api/v10/oauth2/@me") => Authorization(request),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        private async Task<HttpResponseMessage> ExchangeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/x-www-form-urlencoded");
            var form = (await request.Content.ReadAsStringAsync(cancellationToken)).Split('&')
                .Select(pair => pair.Split('='))
                .ToDictionary(pair => WebUtility.UrlDecode(pair[0]), pair => WebUtility.UrlDecode(pair[1]), StringComparer.Ordinal);
            Exchanges.Add(form);
            if (ExchangeStatus != HttpStatusCode.OK)
            {
                return Json(ExchangeStatus, """{"error":"invalid_grant"}""");
            }

            if (form.GetValueOrDefault("code_verifier") != Verifier || !_used.Add(form.GetValueOrDefault("code") ?? ""))
            {
                return Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
            }

            return Garbled
                ? Json(HttpStatusCode.OK, "<html>")
                : Json(HttpStatusCode.OK, $$"""{"access_token":"access-{{_used.Count}}","token_type":"Bearer","expires_in":604800,"refresh_token":"refresh","scope":"identify"}""");
        }

        private HttpResponseMessage Authorization(HttpRequestMessage request)
        {
            Bearers.Add(request.Headers.Authorization is { Scheme: "Bearer" } bearer ? bearer.Parameter! : "");
            var user = User is null ? "" : $$""","user":{"id":"{{User}}","username":"mark","global_name":"Mark"}""";
            return Json(HttpStatusCode.OK, $$"""{"application":{"id":"{{Application}}","name":"Downfall Instinct"},"scopes":["identify"],"expires":"2026-11-30T00:00:00+00:00"{{user}}}""");
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
