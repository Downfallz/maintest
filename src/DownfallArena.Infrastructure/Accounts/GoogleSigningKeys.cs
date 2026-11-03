using System.Security.Cryptography;
using System.Text.Json;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Google's signing keys, read from its published JSON Web Key Set and kept for as long as the answer's
/// <c>Cache-Control: max-age</c> says (an hour when it says nothing). A refresh is refused for a minute after the
/// last fetch, so a stream of tokens naming a key that does not exist cannot make the host ask Google once each.
/// A key handed out is never disposed while the host runs: a verification may still be using it after a refresh
/// has replaced it, and a key is a few hundred bytes the collector takes back once nobody holds it.
/// </summary>
public sealed class GoogleSigningKeys : ISigningKeySource, IDisposable
{
    public static readonly Uri Certificates = new("https://www.googleapis.com/oauth2/v3/certs");

    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan RefreshFloor = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly Uri _location;
    private readonly SemaphoreSlim _fetching = new(1, 1);
    private Dictionary<string, RSA> _keys = [];
    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public GoogleSigningKeys(HttpClient http, TimeProvider clock, Uri? location = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(clock);
        _http = http;
        _clock = clock;
        _location = location ?? Certificates;
    }

    public async Task<RSA?> KeyAsync(string keyId, bool refresh, CancellationToken cancellationToken = default)
    {
        await _fetching.WaitAsync(cancellationToken);
        try
        {
            var now = _clock.GetUtcNow();
            if (now >= _expiresAt || (refresh && now - _fetchedAt >= RefreshFloor))
            {
                await FetchAsync(now, cancellationToken);
            }

            return _keys.GetValueOrDefault(keyId);
        }
        finally
        {
            _fetching.Release();
        }
    }

    public void Dispose()
    {
        foreach (var key in _keys.Values)
        {
            key.Dispose();
        }

        _fetching.Dispose();
    }

    private async Task FetchAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(_location, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        var keys = new Dictionary<string, RSA>(StringComparer.Ordinal);
        foreach (var key in document.RootElement.GetProperty("keys").EnumerateArray())
        {
            if (key.TryGetProperty("kty", out var type) && type.GetString() == "RSA"
                && key.TryGetProperty("kid", out var id) && id.GetString() is { } kid
                && key.TryGetProperty("n", out var modulus) && key.TryGetProperty("e", out var exponent))
            {
                keys[kid] = RSA.Create(new RSAParameters
                {
                    Modulus = Base64Url.Decode(modulus.GetString()!),
                    Exponent = Base64Url.Decode(exponent.GetString()!),
                });
            }
        }

        _keys = keys;
        _fetchedAt = now;
        _expiresAt = now + (response.Headers.CacheControl?.MaxAge ?? DefaultLifetime);
    }
}
