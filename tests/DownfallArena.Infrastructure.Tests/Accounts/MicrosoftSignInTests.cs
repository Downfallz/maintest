using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class MicrosoftSignInTests : IDisposable
{
    private const string ClientId = "6731de76-14a6-49ae-97bc-6eba6914391e";
    private const string Code = "M.C519_BAY.2.U.7b1c2d3e-4f5a-6b7c-8d9e-0f1a2b3c4d5e";
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk.~abc";
    private const string Redirect = "https://play.downfallinstinct.com/signin/microsoft";
    private const string Subject = "AAAAAAAAAAAAAAAAAAAAAIkzqFVrSaSaFHy782bbtaQ";
    private const string Consumers = "9188040d-6c67-4c5b-b112-36a304b66dad";

    private static readonly DateTimeOffset Start = new(2026, 11, 24, 20, 0, 0, TimeSpan.Zero);

    private readonly FakeMicrosoft _microsoft = new();
    private readonly HttpClient _http;
    private readonly MovableClock _clock = new(Start);
    private readonly List<MicrosoftSignIn> _made = [];

    public MicrosoftSignInTests() => _http = new HttpClient(_microsoft);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private MicrosoftSignIn Microsoft(bool loopback = false, TimeSpan? patience = null)
    {
        var microsoft = new MicrosoftSignIn(ClientId, _http, _clock, loopback, patience);
        _made.Add(microsoft);
        return microsoft;
    }

    private static SignInProof Proof(string code = Code, string verifier = Verifier, string redirect = Redirect) =>
        SignInProof.Of((MicrosoftSignIn.CodeField, code), (MicrosoftSignIn.VerifierField, verifier), (MicrosoftSignIn.RedirectField, redirect));

    public void Dispose()
    {
        _made.ForEach(microsoft => microsoft.Dispose());
        _http.Dispose();
    }

    [Fact]
    public async Task A_code_Microsoft_issued_to_this_app_names_the_tokens_subject()
    {
        var microsoft = Microsoft();

        var identity = await microsoft.SignInAsync(Proof(), Cancel);

        identity.ShouldBe(new ExternalIdentity("microsoft", Subject));
        microsoft.Name.ShouldBe("microsoft");
        microsoft.ClientId.ShouldBe(ClientId);
        microsoft.ReturnPath.ShouldBe("/signin/microsoft");
    }

    [Fact]
    public async Task The_exchange_asks_personal_accounts_for_openid_alone_with_the_verifier_and_no_secret()
    {
        await Microsoft().SignInAsync(Proof(), Cancel);

        var exchange = _microsoft.Exchanges.ShouldHaveSingleItem();
        exchange.ShouldBe(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["scope"] = "openid",
            ["grant_type"] = "authorization_code",
            ["code"] = Code,
            ["code_verifier"] = Verifier,
            ["redirect_uri"] = Redirect,
        });
        _microsoft.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task A_code_used_once_already_is_refused()
    {
        var microsoft = Microsoft();
        (await microsoft.SignInAsync(Proof(), Cancel)).ShouldNotBeNull();

        (await microsoft.SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_code_whose_verifier_does_not_match_is_refused()
    {
        _microsoft.Verifier = "another-verifier-another-verifier-another-verifier";

        (await Microsoft().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_issued_to_another_app_is_refused()
    {
        _microsoft.Audience = "00000000-0000-0000-0000-000000000001";

        (await Microsoft().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Theory]
    [InlineData("https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/v2.0", "72f988bf-86f1-41af-91ab-2d7cd011db47")]
    [InlineData("https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/v2.0", Consumers)]
    [InlineData("https://sts.windows.net/9188040d-6c67-4c5b-b112-36a304b66dad/", Consumers)]
    [InlineData("https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0", "72f988bf-86f1-41af-91ab-2d7cd011db47")]
    public async Task A_token_of_a_work_or_school_tenant_or_another_issuer_is_refused(string issuer, string tenant)
    {
        _microsoft.Issuer = issuer;
        _microsoft.Tenant = tenant;

        (await Microsoft().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_client_id_given_in_upper_case_is_the_same_app()
    {
        var microsoft = new MicrosoftSignIn(ClientId.ToUpperInvariant(), _http, _clock);
        _made.Add(microsoft);

        (await microsoft.SignInAsync(Proof(), Cancel)).ShouldBe(new ExternalIdentity("microsoft", Subject));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"iss":""")]
    public async Task Claims_that_cannot_be_read_are_refused_and_not_a_crash(string claims)
    {
        _microsoft.Token = $"e30.{Encode(claims)}.c2ln";

        (await Microsoft().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task An_expiry_beyond_any_date_is_refused_and_not_a_crash()
    {
        _microsoft.Token = $"e30.{Encode($$"""{"iss":"https://login.microsoftonline.com/{{Consumers}}/v2.0","tid":"{{Consumers}}","aud":"{{ClientId}}","sub":"s","exp":1000000000000000}""")}.c2ln";

        (await Microsoft().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    private static string Encode(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public async Task An_expired_token_is_refused()
    {
        _microsoft.Expires = Start - TimeSpan.FromMinutes(2);

        (await Microsoft().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_issued_in_the_future_is_refused()
    {
        _microsoft.Issued = Start + TimeSpan.FromMinutes(5);

        (await Microsoft().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_without_a_subject_is_refused()
    {
        _microsoft.Subject = null;

        (await Microsoft().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.!!!.c")]
    [InlineData("a.bnVsbA.c")]
    public async Task An_id_token_that_cannot_be_read_is_refused(string token)
    {
        _microsoft.Token = token;

        (await Microsoft().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task An_answer_without_an_id_token_is_refused()
    {
        _microsoft.Tokenless = true;

        (await Microsoft().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_code_microsoft_refuses_is_a_refusal()
    {
        _microsoft.ExchangeStatus = HttpStatusCode.BadRequest;

        (await Microsoft().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Theory]
    [InlineData("invalid_client")]
    [InlineData("unauthorized_client")]
    public async Task An_app_microsoft_refuses_is_unavailable_and_said_by_its_error_alone(string error)
    {
        _microsoft.ExchangeStatus = HttpStatusCode.BadRequest;
        _microsoft.Error = error;

        var refused = await Should.ThrowAsync<SignInUnavailableException>(() => Microsoft().SignInAsync(Proof(), Cancel));

        refused.Message.ShouldBe($"Microsoft refuses the app ({error}): see infra/runbooks/sign-ins.md.");
        refused.ProviderAnswered.ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Microsoft_failing_or_asking_the_host_to_slow_down_is_unavailable_not_a_refusal(HttpStatusCode status)
    {
        _microsoft.ExchangeStatus = status;

        (await Should.ThrowAsync<SignInUnavailableException>(() => Microsoft().SignInAsync(Proof(), Cancel))).ProviderAnswered.ShouldBeFalse();
    }

    [Fact]
    public async Task Microsoft_out_of_reach_is_unavailable()
    {
        _microsoft.Unreachable = true;

        await Should.ThrowAsync<SignInUnavailableException>(() => Microsoft().SignInAsync(Proof(), Cancel));
    }

    [Fact]
    public async Task Microsoft_slower_than_the_host_waits_is_unavailable()
    {
        _microsoft.Delay = TimeSpan.FromSeconds(30);

        await Should.ThrowAsync<SignInUnavailableException>(() => Microsoft(patience: TimeSpan.FromMilliseconds(50)).SignInAsync(Proof(), Cancel));
    }

    [Fact]
    public async Task After_a_429_the_host_asks_microsoft_nothing_until_its_retry_after_has_passed()
    {
        var microsoft = Microsoft();
        _microsoft.ExchangeStatus = HttpStatusCode.TooManyRequests;
        _microsoft.RetryAfter = TimeSpan.FromSeconds(20);
        await Should.ThrowAsync<SignInUnavailableException>(() => microsoft.SignInAsync(Proof(), Cancel));
        _microsoft.ExchangeStatus = HttpStatusCode.OK;
        var calls = _microsoft.Calls;

        await Should.ThrowAsync<SignInUnavailableException>(() => microsoft.SignInAsync(Proof(), Cancel));
        _microsoft.Calls.ShouldBe(calls);
        _clock.Advance(TimeSpan.FromSeconds(20));
        (await microsoft.SignInAsync(Proof(), Cancel)).ShouldNotBeNull();
    }

    [Fact]
    public async Task The_host_asks_microsoft_for_a_bounded_number_of_exchanges_a_minute()
    {
        var microsoft = Microsoft();
        _microsoft.ExchangeStatus = HttpStatusCode.BadRequest;
        for (var i = 0; i < MicrosoftSignIn.ExchangesPerMinute; i++)
        {
            (await microsoft.SignInAsync(Proof(code: $"code{i}"), Cancel)).ShouldBeNull();
        }

        await Should.ThrowAsync<SignInUnavailableException>(() => microsoft.SignInAsync(Proof(), Cancel));
        _microsoft.Calls.ShouldBe(MicrosoftSignIn.ExchangesPerMinute);
    }

    [Theory]
    [InlineData("", Verifier, Redirect)]
    [InlineData("a code", Verifier, Redirect)]
    [InlineData("a\ncode", Verifier, Redirect)]
    [InlineData("é", Verifier, Redirect)]
    [InlineData(Code, "too-short", Redirect)]
    [InlineData(Code, Verifier, "https://play.downfallinstinct.com/signin/discord")]
    [InlineData(Code, Verifier, "http://play.downfallinstinct.com/signin/microsoft")]
    [InlineData(Code, Verifier, "https://play.downfallinstinct.com/signin/microsoft?next=/play")]
    [InlineData(Code, Verifier, "http://localhost:5099/signin/microsoft")]
    public async Task A_proof_of_the_wrong_shape_is_refused_without_asking_microsoft(string code, string verifier, string redirect)
    {
        (await Microsoft().SignInAsync(Proof(code, verifier, redirect), Cancel)).ShouldBeNull();

        _microsoft.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task A_code_longer_than_microsoft_hands_out_is_refused_without_asking_microsoft()
    {
        (await Microsoft().SignInAsync(Proof(code: new string('A', 4097)), Cancel)).ShouldBeNull();

        _microsoft.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task A_laptops_table_returns_to_its_loopback_address_over_http()
    {
        (await Microsoft(loopback: true).SignInAsync(Proof(redirect: "http://localhost:5099/signin/microsoft"), Cancel)).ShouldNotBeNull();
    }

    [Fact]
    public void A_client_id_is_required()
    {
        Should.Throw<ArgumentException>(() => new MicrosoftSignIn(" ", _http, _clock));
    }

    /// <summary>
    /// The token endpoint of personal Microsoft accounts as the provider calls it: a code taken once and only with the
    /// verifier its challenge was made from, answered with an ID token whose claims each test can change.
    /// </summary>
    private sealed class FakeMicrosoft : HttpMessageHandler
    {
        private readonly HashSet<string> _used = [];

        public string Verifier { get; set; } = MicrosoftSignInTests.Verifier;

        public string Issuer { get; set; } = $"https://login.microsoftonline.com/{Consumers}/v2.0";

        public string Tenant { get; set; } = Consumers;

        public string Audience { get; set; } = ClientId;

        public string? Subject { get; set; } = MicrosoftSignInTests.Subject;

        public DateTimeOffset Issued { get; set; } = Start;

        public DateTimeOffset Expires { get; set; } = Start + TimeSpan.FromHours(1);

        /// <summary>The ID token answered as it is, instead of one made of the claims above.</summary>
        public string? Token { get; set; }

        /// <summary>Whether the answer holds no ID token at all.</summary>
        public bool Tokenless { get; set; }

        public HttpStatusCode ExchangeStatus { get; set; } = HttpStatusCode.OK;

        public string Error { get; set; } = "invalid_grant";

        public bool Unreachable { get; set; }

        public TimeSpan? RetryAfter { get; set; }

        public TimeSpan Delay { get; set; }

        public int Calls { get; private set; }

        public List<Dictionary<string, string>> Exchanges { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Unreachable)
            {
                throw new HttpRequestException("No route to login.microsoftonline.com.");
            }

            await Task.Delay(Delay, cancellationToken);
            return (request.Method.Method, request.RequestUri!.AbsoluteUri) switch
            {
                ("POST", "https://login.microsoftonline.com/consumers/oauth2/v2.0/token") => await ExchangeAsync(request, cancellationToken),
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
                var refused = Json(ExchangeStatus, $$"""{"error":"{{Error}}","error_description":"AADSTS70000: the player's words."}""");
                if (RetryAfter is { } wait)
                {
                    refused.Headers.RetryAfter = new RetryConditionHeaderValue(wait);
                }

                return refused;
            }

            if (form.GetValueOrDefault("code_verifier") != Verifier || !_used.Add(form.GetValueOrDefault("code") ?? ""))
            {
                return Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
            }

            var token = Tokenless ? "" : $$""","id_token":"{{Token ?? IdToken()}}" """;
            return Json(HttpStatusCode.OK, $$"""{"token_type":"Bearer","scope":"openid","expires_in":3600,"access_token":"EwBo..."{{token}}}""");
        }

        private string IdToken()
        {
            var subject = Subject is null ? "" : $$""","sub":"{{Subject}}" """;
            var claims = $$"""{"ver":"2.0","iss":"{{Issuer}}","aud":"{{Audience}}","tid":"{{Tenant}}","iat":{{Issued.ToUnixTimeSeconds()}},"nbf":{{Issued.ToUnixTimeSeconds()}},"exp":{{Expires.ToUnixTimeSeconds()}}{{subject}}}""";
            return $"{Encode("""{"typ":"JWT","alg":"RS256","kid":"k1"}""")}.{Encode(claims)}.c2lnbmF0dXJl";
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
