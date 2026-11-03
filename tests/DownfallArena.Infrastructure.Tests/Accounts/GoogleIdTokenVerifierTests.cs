using System.Security.Cryptography;
using DownfallArena.Application.Accounts;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class GoogleIdTokenVerifierTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 20, 14, 0, TimeSpan.Zero);

    private readonly TestIdTokens _tokens = new();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private GoogleIdTokenVerifier Verifier() => new(TestIdTokens.ClientId, _tokens, new Clock(Now));

    private Task<ExternalIdentity?> VerifyAsync(string token) => Verifier().VerifyAsync(token, Cancel);

    private Task<ExternalIdentity?> VerifyAsync(Action<Dictionary<string, object>> change)
    {
        var claims = TestIdTokens.Claims(Now);
        change(claims);
        return VerifyAsync(_tokens.Sign(claims));
    }

    [Fact]
    public async Task A_token_Google_signed_for_this_host_names_its_subject()
    {
        (await VerifyAsync(_tokens.Sign(TestIdTokens.Claims(Now)))).ShouldBe(new ExternalIdentity("google", "109876543210987654321"));
    }

    [Fact]
    public async Task The_issuer_may_be_written_without_its_scheme()
    {
        (await VerifyAsync(claims => claims["iss"] = "accounts.google.com")).ShouldNotBeNull();
    }

    [Fact]
    public async Task An_audience_listing_this_host_among_others_is_this_host()
    {
        (await VerifyAsync(claims => claims["aud"] = new[] { "another", TestIdTokens.ClientId })).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_token_for_another_client_is_refused()
    {
        (await VerifyAsync(claims => claims["aud"] = "someone-else.apps.googleusercontent.com")).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_from_another_issuer_is_refused()
    {
        (await VerifyAsync(claims => claims["iss"] = "https://evil.example.com")).ShouldBeNull();
    }

    [Fact]
    public async Task An_expired_token_is_refused_once_the_skew_has_passed()
    {
        (await VerifyAsync(claims => claims["exp"] = Now.AddSeconds(-30).ToUnixTimeSeconds())).ShouldNotBeNull();
        (await VerifyAsync(claims => claims["exp"] = Now.AddMinutes(-2).ToUnixTimeSeconds())).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_issued_in_the_future_is_refused()
    {
        (await VerifyAsync(claims => claims["iat"] = Now.AddMinutes(5).ToUnixTimeSeconds())).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_without_a_subject_or_an_expiry_is_refused()
    {
        (await VerifyAsync(claims => claims.Remove("sub"))).ShouldBeNull();
        (await VerifyAsync(claims => claims.Remove("exp"))).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_signed_by_another_key_is_refused()
    {
        using var other = RSA.Create(2048);

        (await VerifyAsync(_tokens.Sign(TestIdTokens.Claims(Now), key: other))).ShouldBeNull();
    }

    [Fact]
    public async Task A_token_whose_claims_were_changed_after_signing_is_refused()
    {
        var parts = _tokens.Sign(TestIdTokens.Claims(Now)).Split('.');
        var claims = TestIdTokens.Claims(Now);
        claims["sub"] = "1";

        (await VerifyAsync($"{parts[0]}.{TestIdTokens.Encode(claims)}.{parts[2]}")).ShouldBeNull();
    }

    [Theory]
    [InlineData("none")]
    [InlineData("HS256")]
    public async Task A_token_that_names_another_algorithm_is_refused(string algorithm)
    {
        (await VerifyAsync(_tokens.Sign(TestIdTokens.Claims(Now), algorithm: algorithm))).ShouldBeNull();
    }

    [Fact]
    public async Task A_key_not_held_is_asked_for_again_once_and_then_refused()
    {
        (await VerifyAsync(_tokens.Sign(TestIdTokens.Claims(Now), keyId: "rotated"))).ShouldBeNull();

        _tokens.Refreshes.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    [InlineData("!!!.@@@.###")]
    public async Task Text_that_is_not_a_token_is_refused_without_throwing(string text)
    {
        (await VerifyAsync(text)).ShouldBeNull();
    }

    public void Dispose() => _tokens.Dispose();

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
