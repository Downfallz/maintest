using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>ID tokens signed by a key a test makes, and the key source that serves it under its id.</summary>
internal sealed class TestIdTokens : ISigningKeySource, IDisposable
{
    public const string ClientId = "1234-test.apps.googleusercontent.com";
    public const string KeyId = "test-key";

    public RSA Key { get; } = RSA.Create(2048);

    public int Refreshes { get; private set; }

    public Task<RSA?> KeyAsync(string keyId, bool refresh, CancellationToken cancellationToken = default)
    {
        Refreshes += refresh ? 1 : 0;
        return Task.FromResult(keyId == KeyId ? Key : null);
    }

    public static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Encode(object value) => Encode(JsonSerializer.SerializeToUtf8Bytes(value));

    public static Dictionary<string, object> Claims(DateTimeOffset now) => new()
    {
        ["iss"] = "https://accounts.google.com",
        ["aud"] = ClientId,
        ["sub"] = "109876543210987654321",
        ["email"] = "someone@example.com",
        ["iat"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
        ["exp"] = now.AddMinutes(59).ToUnixTimeSeconds(),
    };

    public string Sign(object claims, string keyId = KeyId, string algorithm = "RS256", RSA? key = null)
    {
        var signed = $"{Encode(new { alg = algorithm, kid = keyId, typ = "JWT" })}.{Encode(claims)}";
        var signature = (key ?? Key).SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signed}.{Encode(signature)}";
    }

    public void Dispose() => Key.Dispose();
}
