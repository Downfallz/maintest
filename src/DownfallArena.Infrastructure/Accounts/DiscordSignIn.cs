using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Signs a player in with Discord (sign-in methods plan, P3): the authorization code flow with PKCE, the app a public
/// client, so the host holds no secret. The page sends Discord's code, the verifier its challenge was made from and the
/// address Discord returned to; the host exchanges them at Discord's token endpoint, asks who the token is for, keeps
/// the user's id and nothing else, and revokes the token. The scope is <c>identify</c>: no email.
/// <para>
/// Discord saying no (a forged, reused or foreign code, a token of another app) is a refusal, null. Discord that could
/// not be asked is <see cref="SignInUnavailableException" />: out of reach, slower than <see cref="Patience" />, a
/// server error, a 429, and the host's own holding back. Every route call here reaches Discord from the table's one
/// address, and Discord bans an address that keeps sending it requests it refuses, so the host bounds its calls: a few
/// at a time, <see cref="ExchangesPerMinute" /> a minute, and none until Discord's <c>Retry-After</c> has passed.
/// </para>
/// </summary>
public sealed class DiscordSignIn : ISignInProvider, IDisposable
{
    /// <summary>The field of the posted body that holds the code Discord returned.</summary>
    public const string CodeField = "code";

    /// <summary>The field that holds the PKCE verifier, made by the page and kept in the tab until now.</summary>
    public const string VerifierField = "verifier";

    /// <summary>The field that holds the address Discord returned to, which the exchange repeats.</summary>
    public const string RedirectField = "redirect";

    /// <summary>The path Discord returns to on this host: the only redirect the host accepts.</summary>
    public const string ReturnPath = "/signin/discord";

    /// <summary>How many exchanges the host asks Discord for in a minute, at most: far above a table's sign-ins.</summary>
    public const int ExchangesPerMinute = 30;

    /// <summary>How many sign-ins ask Discord at once, at most.</summary>
    public const int AtOnce = 4;

    /// <summary>Where Discord's API answers, version 10.</summary>
    public static readonly Uri Api = new("https://discord.com/api/v10/");

    /// <summary>How long a sign-in waits for Discord, all its calls together, before it is unavailable.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>How long the host holds back after a 429 that says nothing of when to come back.</summary>
    public static readonly TimeSpan DefaultHoldBack = TimeSpan.FromMinutes(1);

    // Discord asks every client to name itself.
    private static readonly ProductInfoHeaderValue Agent = new("DownfallInstinct", "1.0");

    private const int LongestCode = 256;

    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly bool _loopback;
    private readonly TimeSpan _patience;
    private readonly SemaphoreSlim _asking = new(AtOnce, AtOnce);
    private readonly Lock _gate = new();
    private readonly Queue<DateTimeOffset> _recent = new();
    private DateTimeOffset _heldUntil = DateTimeOffset.MinValue;

    /// <param name="clientId">The Discord application's id, public.</param>
    /// <param name="http">The client the calls go through.</param>
    /// <param name="clock">What the minute's count and a hold-back are read against.</param>
    /// <param name="loopback">
    /// Whether a return to <c>http://localhost</c> is accepted: on a laptop's table only. A hosted table takes https
    /// alone, so a code sent to a listener on a player's own machine cannot sign anybody in there.
    /// </param>
    /// <param name="patience">How long a sign-in waits for Discord; <see cref="Patience" /> by default.</param>
    public DiscordSignIn(string clientId, HttpClient http, TimeProvider clock, bool loopback = false, TimeSpan? patience = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(clock);
        ClientId = clientId;
        _http = http;
        _clock = clock;
        _loopback = loopback;
        _patience = patience ?? Patience;
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

        if (!await _asking.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            throw new SignInUnavailableException("Discord is being asked by as many sign-ins as the host allows at once.");
        }

        try
        {
            Admit();
            using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            patience.CancelAfter(_patience);
            try
            {
                if (await ExchangeAsync(code, verifier, redirect, patience.Token) is not { } token)
                {
                    return null;
                }

                var identity = await WhoAsync(token, patience.Token);
                // Outside the sign-in's patience: once the player is known, nothing the revocation does may undo it.
                await RevokeAsync(token, cancellationToken);
                return identity;
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SignInUnavailableException("Discord did not answer in time.", exception);
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException)
            {
                throw new SignInUnavailableException("Discord could not be asked.", exception);
            }
        }
        finally
        {
            _asking.Release();
        }
    }

    public void Dispose() => _asking.Dispose();

    /// <summary>Counts this exchange against the minute's, or refuses it while the host holds back or the minute is spent.</summary>
    private void Admit()
    {
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            if (now < _heldUntil)
            {
                throw new SignInUnavailableException("Discord asked the host to wait.");
            }

            while (_recent.Count > 0 && now - _recent.Peek() >= TimeSpan.FromMinutes(1))
            {
                _recent.Dequeue();
            }

            if (_recent.Count >= ExchangesPerMinute)
            {
                throw new SignInUnavailableException("The host has asked Discord as often as it allows this minute.");
            }

            _recent.Enqueue(now);
        }
    }

    /// <summary>
    /// What an answer that is not a success means: a 429 holds the host back for as long as Discord says, and it, a
    /// server error or anything Discord gives no verdict with is unavailable; a client error is Discord saying no.
    /// </summary>
    private void Unsuccessful(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var wait = response.Headers.RetryAfter?.Delta ?? DefaultHoldBack;
            lock (_gate)
            {
                _heldUntil = _clock.GetUtcNow() + wait;
            }

            throw new SignInUnavailableException("Discord asked the host to slow down.");
        }

        if ((int)response.StatusCode >= 500 || (int)response.StatusCode < 400)
        {
            throw new SignInUnavailableException($"Discord answered {(int)response.StatusCode}.");
        }
    }

    private async Task<string?> ExchangeAsync(string code, string verifier, string redirect, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, "oauth2/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect,
            ["client_id"] = ClientId!,
        });
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Unsuccessful(response);
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
        using var request = Request(HttpMethod.Get, "oauth2/@me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Unsuccessful(response);
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

    /// <summary>
    /// Gives the token back, so it is good for nothing once the host has read who it is for. Its refresh token goes with
    /// it. Best effort, with a patience of its own: the sign-in is already decided, and a token that could not be revoked
    /// lapses on its own. Only the caller's own cancellation goes through.
    /// </summary>
    private async Task RevokeAsync(string token, CancellationToken caller)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(caller);
        patience.CancelAfter(_patience);
        var cancellationToken = patience.Token;
        try
        {
            using var request = Request(HttpMethod.Post, "oauth2/token/revoke");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["token"] = token,
                ["token_type_hint"] = "access_token",
                ["client_id"] = ClientId!,
            });
            using var response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException && !caller.IsCancellationRequested)
        {
            // Left to lapse: see the summary.
        }
    }

    private static HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(Api, path));
        request.Headers.UserAgent.Add(Agent);
        return request;
    }

    /// <summary>A code as Discord hands it: a short run of URL-safe characters, so nothing else is forwarded.</summary>
    private static bool Code(string code) => code.Length is > 0 and <= LongestCode && code.All(Unreserved);

    /// <summary>A PKCE verifier (RFC 7636): 43 to 128 unreserved characters.</summary>
    private static bool Verifier(string verifier) => verifier.Length is >= 43 and <= 128 && verifier.All(Unreserved);

    private static bool Unreserved(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~';

    /// <summary>
    /// The address Discord returned to: this host's return page, over https, or over http on a laptop's loopback
    /// address when the table itself serves only there. Discord refuses one the app does not list; this keeps the host
    /// from forwarding anything else.
    /// </summary>
    private bool Returning(string redirect) =>
        Uri.TryCreate(redirect, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (_loopback && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        && uri.UserInfo.Length == 0
        && uri.AbsolutePath == ReturnPath && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && uri.OriginalString == uri.GetLeftPart(UriPartial.Path);

    /// <summary>A Discord id: a snowflake, up to twenty digits.</summary>
    private static bool Snowflake(string id) => id.Length is > 0 and <= 20 && id.All(char.IsAsciiDigit);

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
