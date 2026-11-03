using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

public sealed class GoogleSigningKeysTests : IDisposable
{
    private readonly RSA _key = RSA.Create(2048);
    private readonly Keys _handler;
    private readonly HttpClient _http;
    private readonly MovableClock _clock = new(new DateTimeOffset(2026, 11, 3, 20, 0, 0, TimeSpan.Zero));

    public GoogleSigningKeysTests()
    {
        _handler = new Keys(_key);
        _http = new HttpClient(_handler);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_published_key_verifies_what_its_private_half_signed()
    {
        using var keys = new GoogleSigningKeys(_http, _clock);
        var data = "signed"u8.ToArray();
        var signature = _key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var published = (await keys.KeyAsync("k1", refresh: false, Cancel)).ShouldNotBeNull();

        published.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).ShouldBeTrue();
    }

    [Fact]
    public async Task The_keys_are_kept_for_as_long_as_the_answer_says()
    {
        using var keys = new GoogleSigningKeys(_http, _clock);
        await keys.KeyAsync("k1", refresh: false, Cancel);

        _clock.Advance(TimeSpan.FromMinutes(59));
        await keys.KeyAsync("k1", refresh: false, Cancel);
        _handler.Fetches.ShouldBe(1);

        _clock.Advance(TimeSpan.FromMinutes(1));
        await keys.KeyAsync("k1", refresh: false, Cancel);
        _handler.Fetches.ShouldBe(2);
    }

    [Fact]
    public async Task A_refresh_asks_again_but_not_twice_within_a_minute()
    {
        using var keys = new GoogleSigningKeys(_http, _clock);
        (await keys.KeyAsync("unknown", refresh: false, Cancel)).ShouldBeNull();

        await keys.KeyAsync("unknown", refresh: true, Cancel);
        _handler.Fetches.ShouldBe(1);

        _clock.Advance(TimeSpan.FromMinutes(1));
        await keys.KeyAsync("unknown", refresh: true, Cancel);
        _handler.Fetches.ShouldBe(2);
    }

    public void Dispose()
    {
        _http.Dispose();
        _handler.Dispose();
        _key.Dispose();
    }

    private sealed class Keys(RSA key) : HttpMessageHandler
    {
        public int Fetches { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Fetches++;
            var parameters = key.ExportParameters(includePrivateParameters: false);
            var json = $$"""{"keys":[{"kty":"RSA","alg":"RS256","use":"sig","kid":"k1","n":"{{TestIdTokens.Encode(parameters.Modulus!)}}","e":"{{TestIdTokens.Encode(parameters.Exponent!)}}"}]}""";
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            response.Headers.CacheControl = new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromHours(1) };
            return Task.FromResult(response);
        }
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
