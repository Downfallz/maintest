using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
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
    private readonly MovableClock _clock = new(new DateTimeOffset(2026, 11, 23, 20, 0, 0, TimeSpan.Zero));
    private readonly List<DiscordSignIn> _made = [];

    public DiscordSignInTests() => _http = new HttpClient(_discord);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private DiscordSignIn Discord(bool loopback = false, TimeSpan? patience = null)
    {
        var discord = new DiscordSignIn(ClientId, _http, _clock, loopback, patience);
        _made.Add(discord);
        return discord;
    }

    private static SignInProof Proof(string code = Code, string verifier = Verifier, string redirect = Redirect) =>
        SignInProof.Of((DiscordSignIn.CodeField, code), (DiscordSignIn.VerifierField, verifier), (DiscordSignIn.RedirectField, redirect));

    public void Dispose()
    {
        _made.ForEach(discord => discord.Dispose());
        _http.Dispose();
    }

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
        _discord.Agents.ShouldAllBe(agent => agent.StartsWith("DownfallInstinct/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_token_is_given_back_once_the_user_is_read()
    {
        await Discord().SignInAsync(Proof(), Cancel);

        var revoked = _discord.Revoked.ShouldHaveSingleItem();
        revoked["token"].ShouldBe("access-1");
        revoked["client_id"].ShouldBe(ClientId);
    }

    [Fact]
    public async Task A_revocation_slower_than_the_host_waits_still_signs_the_player_in()
    {
        _discord.RevokeDelay = TimeSpan.FromSeconds(30);

        (await Discord(patience: TimeSpan.FromMilliseconds(200)).SignInAsync(Proof(), Cancel)).ShouldBe(new ExternalIdentity("discord", UserId));
    }

    [Fact]
    public async Task A_token_discord_will_not_take_back_still_signs_the_player_in()
    {
        _discord.RevokeStatus = HttpStatusCode.ServiceUnavailable;

        (await Discord().SignInAsync(Proof(), Cancel)).ShouldNotBeNull();
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

    [Fact]
    public async Task A_code_discord_refuses_is_a_refusal()
    {
        _discord.ExchangeStatus = HttpStatusCode.BadRequest;

        (await Discord().SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task An_app_discord_refuses_is_unavailable_not_a_refusal_of_the_player()
    {
        _discord.ExchangeStatus = HttpStatusCode.Unauthorized;
        _discord.Error = "invalid_client";

        var refused = await Should.ThrowAsync<SignInUnavailableException>(() => Discord().SignInAsync(Proof(), Cancel));

        refused.Message.ShouldBe("Discord refuses the app (invalid_client): see infra/runbooks/sign-ins.md.");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Discord_failing_or_asking_the_host_to_slow_down_is_unavailable_not_a_refusal(HttpStatusCode status)
    {
        _discord.ExchangeStatus = status;

        await Should.ThrowAsync<SignInUnavailableException>(() => Discord().SignInAsync(Proof(), Cancel));
    }

    [Fact]
    public async Task An_answer_that_is_not_json_is_unavailable()
    {
        _discord.Garbled = true;

        await Should.ThrowAsync<SignInUnavailableException>(() => Discord().SignInAsync(Proof(), Cancel));
    }

    [Fact]
    public async Task Discord_out_of_reach_is_unavailable()
    {
        _discord.Unreachable = true;

        await Should.ThrowAsync<SignInUnavailableException>(() => Discord().SignInAsync(Proof(), Cancel));
    }

    [Fact]
    public async Task Discord_slower_than_the_host_waits_is_unavailable()
    {
        _discord.Delay = TimeSpan.FromSeconds(30);

        await Should.ThrowAsync<SignInUnavailableException>(() => Discord(patience: TimeSpan.FromMilliseconds(50)).SignInAsync(Proof(), Cancel));
    }

    [Fact]
    public void The_host_waits_ten_seconds_for_discord_by_default()
    {
        DiscordSignIn.Patience.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task After_a_429_the_host_asks_discord_nothing_until_its_retry_after_has_passed()
    {
        var discord = Discord();
        _discord.ExchangeStatus = HttpStatusCode.TooManyRequests;
        _discord.RetryAfter = TimeSpan.FromSeconds(20);
        await Should.ThrowAsync<SignInUnavailableException>(() => discord.SignInAsync(Proof(), Cancel));
        _discord.ExchangeStatus = HttpStatusCode.OK;
        var calls = _discord.Calls;

        await Should.ThrowAsync<SignInUnavailableException>(() => discord.SignInAsync(Proof(), Cancel));
        _discord.Calls.ShouldBe(calls);
        _clock.Advance(TimeSpan.FromSeconds(20));
        (await discord.SignInAsync(Proof(), Cancel)).ShouldNotBeNull();
    }

    [Fact]
    public async Task The_host_asks_discord_for_a_bounded_number_of_exchanges_a_minute()
    {
        var discord = Discord();
        _discord.ExchangeStatus = HttpStatusCode.BadRequest;
        for (var i = 0; i < DiscordSignIn.ExchangesPerMinute; i++)
        {
            (await discord.SignInAsync(Proof(code: $"code{i}"), Cancel)).ShouldBeNull();
        }

        await Should.ThrowAsync<SignInUnavailableException>(() => discord.SignInAsync(Proof(), Cancel));
        _discord.Calls.ShouldBe(DiscordSignIn.ExchangesPerMinute);
        _clock.Advance(TimeSpan.FromMinutes(1));
        (await discord.SignInAsync(Proof(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task Only_a_few_sign_ins_ask_discord_at_once()
    {
        var discord = Discord();
        _discord.Held = new TaskCompletionSource();
        var asking = Enumerable.Range(0, DiscordSignIn.AtOnce).Select(i => discord.SignInAsync(Proof(code: $"code{i}"), Cancel)).ToList();

        var busy = await Should.ThrowAsync<SignInUnavailableException>(() => discord.SignInAsync(Proof(), Cancel));
        _discord.Held.SetResult();
        busy.RetryAfter.ShouldBe(TimeSpan.FromSeconds(2), "the code was never sent: the page may post it again shortly (#1204)");
        (await Task.WhenAll(asking)).ShouldAllBe(identity => identity != null);
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
        (await Discord(loopback: true).SignInAsync(Proof(redirect: "http://localhost:5099/signin/discord"), Cancel)).ShouldNotBeNull();
        (await Discord(loopback: true).SignInAsync(Proof(code: "second", redirect: "http://127.0.0.1:5099/signin/discord"), Cancel)).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("http://localhost:5099/signin/discord")]
    [InlineData("http://127.0.0.1:5099/signin/discord")]
    public async Task A_hosted_table_refuses_a_return_to_a_players_own_machine(string redirect)
    {
        (await Discord().SignInAsync(Proof(redirect: redirect), Cancel)).ShouldBeNull();

        _discord.Calls.ShouldBe(0);
    }

    [Fact]
    public void A_client_id_is_required()
    {
        Should.Throw<ArgumentException>(() => new DiscordSignIn(" ", _http, _clock));
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

        public string Error { get; set; } = "invalid_grant";

        public bool Garbled { get; set; }

        public bool Unreachable { get; set; }

        public TimeSpan? RetryAfter { get; set; }

        public TimeSpan Delay { get; set; }

        public TaskCompletionSource? Held { get; set; }

        public HttpStatusCode RevokeStatus { get; set; } = HttpStatusCode.OK;

        public TimeSpan RevokeDelay { get; set; }

        public int Calls { get; private set; }

        public List<Dictionary<string, string>> Revoked { get; } = [];

        public List<string> Agents { get; } = [];

        public List<Dictionary<string, string>> Exchanges { get; } = [];

        public List<string> Bearers { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Agents.Add(request.Headers.UserAgent.ToString());
            if (Unreachable)
            {
                throw new HttpRequestException("No route to discord.com.");
            }

            await Task.Delay(Delay, cancellationToken);
            if (Held is { } held)
            {
                await held.Task.WaitAsync(cancellationToken);
            }

            return (request.Method.Method, request.RequestUri!.AbsoluteUri) switch
            {
                ("POST", "https://discord.com/api/v10/oauth2/token") => await ExchangeAsync(request, cancellationToken),
                ("POST", "https://discord.com/api/v10/oauth2/token/revoke") => await RevokeAsync(request, cancellationToken),
                ("GET", "https://discord.com/api/v10/oauth2/@me") => Authorization(request),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        private static async Task<Dictionary<string, string>> FormAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/x-www-form-urlencoded");
            return (await request.Content.ReadAsStringAsync(cancellationToken)).Split('&')
                .Select(pair => pair.Split('='))
                .ToDictionary(pair => WebUtility.UrlDecode(pair[0]), pair => WebUtility.UrlDecode(pair[1]), StringComparer.Ordinal);
        }

        private async Task<HttpResponseMessage> RevokeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Revoked.Add(await FormAsync(request, cancellationToken));
            await Task.Delay(RevokeDelay, cancellationToken);
            return new HttpResponseMessage(RevokeStatus);
        }

        private async Task<HttpResponseMessage> ExchangeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var form = await FormAsync(request, cancellationToken);
            Exchanges.Add(form);
            if (ExchangeStatus != HttpStatusCode.OK)
            {
                var refused = Json(ExchangeStatus, $$"""{"error":"{{Error}}"}""");
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

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
