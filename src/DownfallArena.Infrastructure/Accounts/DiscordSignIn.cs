using System.Net.Http.Headers;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Signs a player in with Discord (sign-in methods plan, P3): the authorization code flow with PKCE, the app a public
/// client, so the host holds no secret. The page sends Discord's code, the verifier its challenge was made from and the
/// address Discord returned to; the host exchanges them at Discord's token endpoint, asks who the token is for, and
/// keeps the user's id, nothing else. The scope is <c>identify</c>: no email. A token issued to another app is refused,
/// and so is anything Discord does not answer with a token and a user, whatever the reason, so a refusal is never an
/// exception. The access token is used once and kept nowhere.
/// </summary>
public sealed class DiscordSignIn : ISignInProvider
{
    /// <summary>The field of the posted body that holds the code Discord returned.</summary>
    public const string CodeField = "code";

    /// <summary>The field that holds the PKCE verifier, made by the page and kept in the tab until now.</summary>
    public const string VerifierField = "verifier";

    /// <summary>The field that holds the address Discord returned to, which the exchange repeats.</summary>
    public const string RedirectField = "redirect";

    /// <summary>The path Discord returns to on this host: the only redirect the host accepts.</summary>
    public const string ReturnPath = "/signin/discord";

    /// <summary>Where Discord's API answers, version 10.</summary>
    public static readonly Uri Api = new("https://discord.com/api/v10/");

    private const int LongestCode = 256;

    private readonly HttpClient _http;

    public DiscordSignIn(string clientId, HttpClient http)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(http);
        ClientId = clientId;
        _http = http;
    }

    public string Name => ExternalIdentity.Discord;

    public string? ClientId { get; }

    public async Task<ExternalIdentity?> SignInAsync(SignInProof proof, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proof);
        if (proof[CodeField] is not { } code || !Code(code)
            || proof[VerifierField] is not { } verifier || !Verifier(verifier)
            || proof[RedirectField] is not { } redirect || !Returning(redirect))
        {
            return null;
        }

        try
        {
            return await ExchangeAsync(code, verifier, redirect, cancellationToken) is { } token
                ? await WhoAsync(token, cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Discord out of reach, an answer that is not one, or the client's timeout: the sign-in could not be checked.
            return null;
        }
    }

    private async Task<string?> ExchangeAsync(string code, string verifier, string redirect, CancellationToken cancellationToken)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect,
            ["client_id"] = ClientId!,
        });
        using var response = await _http.PostAsync(new Uri(Api, "oauth2/token"), form, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return Text(answer.RootElement, "access_token") is { Length: > 0 } token
            && string.Equals(Text(answer.RootElement, "token_type"), "Bearer", StringComparison.OrdinalIgnoreCase)
                ? token
                : null;
    }

    private async Task<ExternalIdentity?> WhoAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Api, "oauth2/@me"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = answer.RootElement;
        // The token is this app's, or the code was not issued to it: Discord names the app the token was issued to.
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("application", out var application) || Text(application, "id") != ClientId
            || !root.TryGetProperty("user", out var user) || Text(user, "id") is not { } id || !Snowflake(id))
        {
            return null;
        }

        return new ExternalIdentity(ExternalIdentity.Discord, id);
    }

    /// <summary>A code as Discord hands it: a short run of URL-safe characters, so nothing else is forwarded.</summary>
    private static bool Code(string code) => code.Length is > 0 and <= LongestCode && code.All(Unreserved);

    /// <summary>A PKCE verifier (RFC 7636): 43 to 128 unreserved characters.</summary>
    private static bool Verifier(string verifier) => verifier.Length is >= 43 and <= 128 && verifier.All(Unreserved);

    private static bool Unreserved(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~';

    /// <summary>
    /// The address Discord returned to: this host's return page, over https, or over http on a laptop's own loopback
    /// address. Discord refuses one the app does not list; this keeps the host from forwarding anything else.
    /// </summary>
    private static bool Returning(string redirect) =>
        Uri.TryCreate(redirect, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        && uri.UserInfo.Length == 0
        && uri.AbsolutePath == ReturnPath && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && uri.OriginalString == uri.GetLeftPart(UriPartial.Path);

    /// <summary>A Discord id: a snowflake, up to twenty digits.</summary>
    private static bool Snowflake(string id) => id.Length is > 0 and <= 20 && id.All(char.IsAsciiDigit);

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
