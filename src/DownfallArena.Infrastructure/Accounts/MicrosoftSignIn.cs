using System.Text.Json;
using DownfallArena.Application.Accounts;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Signs a player in with a personal Microsoft account (sign-in methods plan, P6), by the code flow with PKCE that
/// <see cref="CodeExchangeSignIn" /> shares, the app registered as a public client: no secret. The host exchanges the
/// code at the <c>consumers</c> token endpoint with the scope <c>openid</c> alone, so the answer's ID token carries no
/// name and no email, and keeps the token's <c>sub</c>, which Microsoft makes for this app alone.
/// <para>
/// The ID token is read from Microsoft's own answer to the host's request over TLS, so its signature is not checked
/// again (OpenID Connect Core 1.0, 3.1.3.7, step 6); its claims are: the issuer and the tenant are the one of personal
/// accounts, the audience is this app, it has not expired. The page's verifier binds the code to the tab that asked for
/// it, so no nonce is needed. The access token that comes with it is for Microsoft's user info, which the host never
/// asks, and it lapses on its own: Microsoft offers no way to give it back.
/// </para>
/// </summary>
public sealed class MicrosoftSignIn : CodeExchangeSignIn
{
    /// <summary>The tenant every personal Microsoft account belongs to.</summary>
    public const string ConsumersTenant = "9188040d-6c67-4c5b-b112-36a304b66dad";

    /// <summary>Where personal Microsoft accounts exchange a code, version 2.0.</summary>
    public static readonly Uri TokenEndpoint = new("https://login.microsoftonline.com/consumers/oauth2/v2.0/token");

    private const string Issuer = $"https://login.microsoftonline.com/{ConsumersTenant}/v2.0";

    // Microsoft documents no alphabet or length for its codes: any visible ASCII, form-encoded when it is posted.
    private const int LongestCode = 4096;

    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(1);

    /// <inheritdoc cref="CodeExchangeSignIn(string, string, string, HttpClient, TimeProvider, bool, TimeSpan?)" />
    public MicrosoftSignIn(string clientId, HttpClient http, TimeProvider clock, bool loopback = false, TimeSpan? patience = null)
        : base(ExternalIdentity.Microsoft, "Microsoft", clientId, http, clock, loopback, patience)
    {
    }

    protected override async Task<ExternalIdentity?> AskAsync(string code, string verifier, string redirect, CancellationToken patience, CancellationToken caller)
    {
        using var answer = await PostAsync(TokenEndpoint, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client_id"] = ClientId!,
            ["scope"] = "openid",
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect,
        }, patience);
        return answer is not null && Text(answer.RootElement, "id_token") is { } token ? Admitted(token) : null;
    }

    protected override bool Code(string code) => code.Length is > 0 and <= LongestCode && code.All(c => c is > ' ' and <= '~');

    private ExternalIdentity? Admitted(string token)
    {
        if (token.Split('.') is not [_, var payload, _])
        {
            return null;
        }

        try
        {
            using var claims = JsonDocument.Parse(Base64Url.Decode(payload));
            var root = claims.RootElement;
            var now = Now;
            if (root.ValueKind != JsonValueKind.Object
                || Text(root, "iss") != Issuer
                || Text(root, "tid") != ConsumersTenant
                || Text(root, "aud") != ClientId
                || Time(root, "exp") is not { } expires || expires + Skew <= now
                || (Time(root, "iat") is { } issued && issued - Skew > now)
                || Text(root, "sub") is not { Length: > 0 } subject)
            {
                return null;
            }

            return new ExternalIdentity(ExternalIdentity.Microsoft, subject);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static DateTimeOffset? Time(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
