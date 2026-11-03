using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Checks a Google ID token the way Google says to (ADR 0122): an RS256 signature by one of Google's keys, an
/// issuer that is Google's, an audience that is this host's client id, and an expiry still ahead (a minute of
/// clock skew either side). Everything else about the token is ignored, the email included: the account is the
/// subject. Written against <see cref="RSA" /> rather than a JWT package: one algorithm and four claims.
/// </summary>
public sealed class GoogleIdTokenVerifier : IIdentityTokenVerifier
{
    private static readonly string[] Issuers = ["accounts.google.com", "https://accounts.google.com"];
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(1);

    private readonly string _clientId;
    private readonly ISigningKeySource _keys;
    private readonly TimeProvider _clock;

    public GoogleIdTokenVerifier(string clientId, ISigningKeySource keys, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(clock);
        _clientId = clientId;
        _keys = keys;
        _clock = clock;
    }

    public async Task<ExternalIdentity?> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token) || token.Split('.') is not [var header, var payload, var signature])
        {
            return null;
        }

        try
        {
            using var head = JsonDocument.Parse(Base64Url.Decode(header));
            if (Text(head.RootElement, "alg") != "RS256" || Text(head.RootElement, "kid") is not { } keyId)
            {
                return null;
            }

            var signed = Encoding.ASCII.GetBytes($"{header}.{payload}");
            var bytes = Base64Url.Decode(signature);
            if (!await SignedAsync(keyId, signed, bytes, cancellationToken))
            {
                return null;
            }

            using var claims = JsonDocument.Parse(Base64Url.Decode(payload));
            return Admitted(claims.RootElement);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or CryptographicException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private async Task<bool> SignedAsync(string keyId, byte[] signed, byte[] signature, CancellationToken cancellationToken)
    {
        var key = await _keys.KeyAsync(keyId, refresh: false, cancellationToken) ?? await _keys.KeyAsync(keyId, refresh: true, cancellationToken);
        return key is not null && key.VerifyData(signed, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private ExternalIdentity? Admitted(JsonElement claims)
    {
        var now = _clock.GetUtcNow();
        if (!Issuers.Contains(Text(claims, "iss"), StringComparer.Ordinal)
            || !Audience(claims)
            || Time(claims, "exp") is not { } expires || expires + Skew <= now
            || (Time(claims, "iat") is { } issued && issued - Skew > now)
            || Text(claims, "sub") is not { Length: > 0 } subject)
        {
            return null;
        }

        return new ExternalIdentity(ExternalIdentity.Google, subject);
    }

    private bool Audience(JsonElement claims) =>
        claims.TryGetProperty("aud", out var audience) && audience.ValueKind switch
        {
            JsonValueKind.String => audience.GetString() == _clientId,
            JsonValueKind.Array => audience.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == _clientId),
            _ => false,
        };

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? Time(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
