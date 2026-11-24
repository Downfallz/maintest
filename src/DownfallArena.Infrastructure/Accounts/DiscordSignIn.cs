using System.Net.Http.Headers;
using System.Text.Json;
using DownfallArena.Application.Accounts;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Signs a player in with Discord (sign-in methods plan, P3), by the code flow with PKCE that
/// <see cref="CodeExchangeSignIn" /> shares. The host exchanges the code at Discord's token endpoint, asks who the token
/// is for, keeps the user's id and nothing else, and revokes the token. The scope is <c>identify</c>: no email.
/// </summary>
public sealed class DiscordSignIn : CodeExchangeSignIn
{
    /// <summary>Where Discord's API answers, version 10.</summary>
    public static readonly Uri Api = new("https://discord.com/api/v10/");

    private const int LongestCode = 256;

    /// <inheritdoc cref="CodeExchangeSignIn(string, string, string, HttpClient, TimeProvider, bool, TimeSpan?)" />
    public DiscordSignIn(string clientId, HttpClient http, TimeProvider clock, bool loopback = false, TimeSpan? patience = null)
        : base(ExternalIdentity.Discord, "Discord", clientId, http, clock, loopback, patience)
    {
    }

    protected override async Task<ExternalIdentity?> AskAsync(string code, string verifier, string redirect, CancellationToken patience, CancellationToken caller)
    {
        if (await ExchangeAsync(code, verifier, redirect, patience) is not { } token)
        {
            return null;
        }

        var identity = await WhoAsync(token, patience);
        // Outside the sign-in's patience: once the player is known, nothing the revocation does may undo it.
        await RevokeAsync(token, caller);
        return identity;
    }

    /// <summary>A code as Discord hands it: a short run of URL-safe characters, so nothing else is forwarded.</summary>
    protected override bool Code(string code) => code.Length is > 0 and <= LongestCode && code.All(Unreserved);

    private async Task<string?> ExchangeAsync(string code, string verifier, string redirect, CancellationToken cancellationToken)
    {
        using var answer = await PostAsync(new Uri(Api, "oauth2/token"), new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect,
            ["client_id"] = ClientId!,
        }, cancellationToken);
        return answer is not null
            && Text(answer.RootElement, "access_token") is { Length: > 0 } token
            && string.Equals(Text(answer.RootElement, "token_type"), "Bearer", StringComparison.OrdinalIgnoreCase)
                ? token
                : null;
    }

    private async Task<ExternalIdentity?> WhoAsync(string token, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, new Uri(Api, "oauth2/@me"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var answer = await SendAsync(request, cancellationToken);
        if (answer is null)
        {
            return null;
        }

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

    /// <summary>
    /// Gives the token back, so it is good for nothing once the host has read who it is for. Its refresh token goes with
    /// it. Best effort: the sign-in is already decided, and a token that could not be revoked lapses on its own.
    /// </summary>
    private async Task RevokeAsync(string token, CancellationToken caller)
    {
        using var request = Request(HttpMethod.Post, new Uri(Api, "oauth2/token/revoke"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["token"] = token,
            ["token_type_hint"] = "access_token",
            ["client_id"] = ClientId!,
        });
        await SendQuietlyAsync(request, caller);
    }

    /// <summary>A Discord id: a snowflake, up to twenty digits.</summary>
    private static bool Snowflake(string id) => id.Length is > 0 and <= 20 && id.All(char.IsAsciiDigit);
}
