using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// A way in by the authorization code flow with PKCE, the app a public client, so the host holds no secret (sign-in
/// methods plan, P3 and P6): Discord and Microsoft. The page sends the provider's code, the verifier its challenge was
/// made from and the address the provider returned to; the provider subclass exchanges them and says who the player is.
/// What every such provider shares is here: the proof's shape checked before the provider is asked, and the bounds on
/// asking it.
/// <para>
/// The provider saying no (a forged, reused or foreign code, a token of another app) is a refusal, null. A provider that
/// could not be asked is <see cref="SignInUnavailableException" />: out of reach, slower than <see cref="Patience" />, a
/// server error, a 429, an app the provider no longer accepts, and the host's own holding back. Every route call here
/// reaches the provider from the table's one address, and a provider may ban an address that keeps sending it requests
/// it refuses, so the host bounds its calls: a few at a time, <see cref="ExchangesPerMinute" /> a minute, and none
/// until the provider's <c>Retry-After</c> has passed.
/// </para>
/// </summary>
public abstract class CodeExchangeSignIn : ISignInProvider, IDisposable
{
    /// <summary>The field of the posted body that holds the code the provider returned.</summary>
    public const string CodeField = "code";

    /// <summary>The field that holds the PKCE verifier, made by the page and kept in the tab until now.</summary>
    public const string VerifierField = "verifier";

    /// <summary>The field that holds the address the provider returned to, which the exchange repeats.</summary>
    public const string RedirectField = "redirect";

    /// <summary>How many exchanges the host asks the provider for in a minute, at most: far above a table's sign-ins.</summary>
    public const int ExchangesPerMinute = 30;

    /// <summary>How many sign-ins ask the provider at once, at most.</summary>
    public const int AtOnce = 4;

    /// <summary>How long a sign-in waits for the provider, all its calls together, before it is unavailable.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>How long the host holds back after a 429 that says nothing of when to come back.</summary>
    public static readonly TimeSpan DefaultHoldBack = TimeSpan.FromMinutes(1);

    // OAuth's errors (RFC 6749, 5.2) that say the app itself is refused, not the player's code: the owner's to fix, so
    // unavailable and said on the console, never a refusal counted against the player's address.
    private static readonly string[] AppRefused = ["invalid_client", "unauthorized_client"];

    // Discord asks every client to name itself; Microsoft takes the same.
    private static readonly ProductInfoHeaderValue Agent = new("DownfallInstinct", "1.0");

    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly bool _loopback;
    private readonly TimeSpan _patience;
    private readonly SemaphoreSlim _asking = new(AtOnce, AtOnce);
    private readonly Lock _gate = new();
    private readonly Queue<DateTimeOffset> _recent = new();
    private DateTimeOffset _heldUntil = DateTimeOffset.MinValue;

    /// <param name="name">The provider's name, as <c>--sign-in</c> lists it.</param>
    /// <param name="label">The provider as a sentence names it, on the console.</param>
    /// <param name="clientId">The app's id at the provider, public.</param>
    /// <param name="http">The client the calls go through.</param>
    /// <param name="clock">What the minute's count and a hold-back are read against.</param>
    /// <param name="loopback">
    /// Whether a return to <c>http://localhost</c> is accepted: on a laptop's table only. A hosted table takes https
    /// alone, so a code sent to a listener on a player's own machine cannot sign anybody in there.
    /// </param>
    /// <param name="patience">How long a sign-in waits for the provider; <see cref="Patience" /> by default.</param>
    protected CodeExchangeSignIn(string name, string label, string clientId, HttpClient http, TimeProvider clock, bool loopback, TimeSpan? patience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(clock);
        Name = name;
        Label = label;
        ClientId = clientId;
        _http = http;
        _clock = clock;
        _loopback = loopback;
        _patience = patience ?? Patience;
    }

    public string Name { get; }

    public string? ClientId { get; }

    /// <summary>The path the provider returns to on this host, <c>/signin/&lt;name&gt;</c>: the only redirect accepted.</summary>
    public string ReturnPath => $"/signin/{Name}";

    /// <summary>What the clock reads now, for the claims a provider's token carries.</summary>
    protected DateTimeOffset Now => _clock.GetUtcNow();

    /// <summary>The provider as a sentence names it.</summary>
    protected string Label { get; }

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
            throw new SignInUnavailableException($"{Label} is being asked by as many sign-ins as the host allows at once.");
        }

        try
        {
            Admit();
            using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            patience.CancelAfter(_patience);
            try
            {
                return await AskAsync(code, verifier, redirect, patience.Token, cancellationToken);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SignInUnavailableException($"{Label} did not answer in time.", exception);
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException)
            {
                throw new SignInUnavailableException($"{Label} could not be asked.", exception);
            }
        }
        finally
        {
            _asking.Release();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asks the provider who the code is for, within the sign-in's <paramref name="patience" />: the identity, or null
    /// when the provider said no. <paramref name="caller" /> is the request's own cancellation, for what comes after the
    /// player is known.
    /// </summary>
    protected abstract Task<ExternalIdentity?> AskAsync(string code, string verifier, string redirect, CancellationToken patience, CancellationToken caller);

    /// <summary>Whether a code is one this provider hands out, so nothing else is forwarded to it.</summary>
    protected abstract bool Code(string code);

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _asking.Dispose();
        }
    }

    /// <summary>A request to the provider, naming the host.</summary>
    protected static HttpRequestMessage Request(HttpMethod method, Uri address)
    {
        var request = new HttpRequestMessage(method, address);
        request.Headers.UserAgent.Add(Agent);
        return request;
    }

    /// <summary>A form posted to the provider: its answer read as JSON, or null when the provider said no.</summary>
    protected async Task<JsonDocument?> PostAsync(Uri address, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, address);
        request.Content = new FormUrlEncodedContent(form);
        return await SendAsync(request, cancellationToken);
    }

    /// <summary>A request sent to the provider: its answer read as JSON, or null when the provider said no.</summary>
    protected async Task<JsonDocument?> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Unsuccessful(response, body);
            return null;
        }

        return JsonDocument.Parse(body);
    }

    /// <summary>Sends a request whose answer does not matter: best effort, within its own patience.</summary>
    protected async Task SendQuietlyAsync(HttpRequestMessage request, CancellationToken caller)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(caller);
        patience.CancelAfter(_patience);
        try
        {
            using var response = await _http.SendAsync(request, patience.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException && !caller.IsCancellationRequested)
        {
            // The answer did not matter: see the summary.
        }
    }

    protected static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Counts this exchange against the minute's, or refuses it while the host holds back or the minute is spent.</summary>
    private void Admit()
    {
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            if (now < _heldUntil)
            {
                throw new SignInUnavailableException($"{Label} asked the host to wait.");
            }

            while (_recent.Count > 0 && now - _recent.Peek() >= TimeSpan.FromMinutes(1))
            {
                _recent.Dequeue();
            }

            if (_recent.Count >= ExchangesPerMinute)
            {
                throw new SignInUnavailableException($"The host has asked {Label} as often as it allows this minute.");
            }

            _recent.Enqueue(now);
        }
    }

    /// <summary>
    /// What an answer that is not a success means: a 429 holds the host back for as long as the provider says, and it,
    /// a server error, an app refused or anything the provider gives no verdict with is unavailable; any other client
    /// error is the provider saying no.
    /// </summary>
    private void Unsuccessful(HttpResponseMessage response, string body)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var wait = response.Headers.RetryAfter?.Delta ?? DefaultHoldBack;
            lock (_gate)
            {
                _heldUntil = _clock.GetUtcNow() + wait;
            }

            throw new SignInUnavailableException($"{Label} asked the host to slow down.");
        }

        if ((int)response.StatusCode is >= 500 or < 400)
        {
            throw new SignInUnavailableException($"{Label} answered {(int)response.StatusCode}.");
        }

        if (ErrorOf(body) is { } error && AppRefused.Contains(error, StringComparer.Ordinal))
        {
            // Only a word of the fixed list is said: the rest of the provider's answer never reaches the console.
            throw new SignInUnavailableException($"{Label} refuses the app ({error}): see infra/runbooks/sign-ins.md.");
        }
    }

    private static string? ErrorOf(string body)
    {
        try
        {
            using var answer = JsonDocument.Parse(body);
            return Text(answer.RootElement, "error");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A PKCE verifier (RFC 7636): 43 to 128 unreserved characters.</summary>
    private static bool Verifier(string verifier) => verifier.Length is >= 43 and <= 128 && verifier.All(Unreserved);

    /// <summary>A character RFC 3986 leaves unreserved: the alphabet of a verifier, and of Discord's codes.</summary>
    protected static bool Unreserved(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~';

    /// <summary>
    /// The address the provider returned to: this host's return page, over https, or over http on a laptop's loopback
    /// address when the table itself serves only there. The provider refuses one the app does not list; this keeps the
    /// host from forwarding anything else.
    /// </summary>
    private bool Returning(string redirect) =>
        Uri.TryCreate(redirect, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (_loopback && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        && uri.UserInfo.Length == 0
        && uri.AbsolutePath == ReturnPath && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && uri.OriginalString == uri.GetLeftPart(UriPartial.Path);
}
