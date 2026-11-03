using System.Collections.Concurrent;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Studio;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Signing in and registering (ADR 0122): Google's ID token in, the host's own session cookie out, and a name chosen
/// in between when the sign-in has no account yet. <c>--accounts dev</c> skips Google and signs in by a typed name,
/// on a loopback host only. Every route is behind the table's same-origin fence, which is what makes a cookie safe
/// to act on: another site cannot send this host a JSON write.
/// </summary>
internal sealed class AccountApi
{
    public const string Prefix = "/api/account";

    private const string NamesPrefix = Prefix + "/names/";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// How long an account read from the store answers for it. Every request of a seat checks its session once
    /// seats belong to accounts (ADR 0122), and a page polls; this is what keeps that off the store. A change made
    /// by this host (signing out everywhere) replaces the entry at once; one replica is the most the table runs.
    /// </summary>
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<AccountId, (Account Account, DateTimeOffset Read)> _cache = new();

    private readonly AccountRegistry _registry;
    private readonly AccountSessions _sessions;
    private readonly IIdentityTokenVerifier? _verifier;

    public AccountApi(AccountRegistry registry, AccountSessions sessions, AccountsMode mode, IIdentityTokenVerifier? verifier = null, string? googleClientId = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(sessions);
        if (mode == AccountsMode.Off)
        {
            throw new ArgumentException("A host with accounts off has no account API.", nameof(mode));
        }

        if (mode == AccountsMode.Google && (verifier is null || string.IsNullOrWhiteSpace(googleClientId)))
        {
            throw new ArgumentException("A Google sign-in needs its verifier and its client id.", nameof(verifier));
        }

        _registry = registry;
        _sessions = sessions;
        _verifier = verifier;
        Mode = mode;
        GoogleClientId = mode == AccountsMode.Google ? googleClientId : null;
    }

    public AccountsMode Mode { get; }

    /// <summary>The Google client id the page's sign-in button is drawn for; public, as Google's own pages show it.</summary>
    public string? GoogleClientId { get; }

    /// <summary>How <c>/api/me</c> names the mode to a page.</summary>
    public static string Named(AccountsMode mode) => mode switch
    {
        AccountsMode.Google => "google",
        AccountsMode.Development => "dev",
        _ => "off",
    };

    public static bool Names(string path) => path == Prefix || path.StartsWith(Prefix + "/", StringComparison.Ordinal);

    /// <summary>
    /// The account a session cookie proves, or null: a cookie this host did not sign, one older than its lifetime,
    /// one issued before the account signed out everywhere, or one whose account is gone.
    /// </summary>
    public async Task<SignedIn?> WhoAsync(AccountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_sessions.Read(request.Session) is not { } claim
            || await AccountAsync(claim.Account, cancellationToken) is not { } account
            || account.Generation != claim.Generation)
        {
            return null;
        }

        var renewed = _sessions.DueForRenewal(claim)
            ? AccountSessions.SetCookie(_sessions.Issue(account), request.Secure)
            : null;
        return new SignedIn(account, renewed);
    }

    /// <summary>The account by its id, from the cache while it is fresh: what a seat's owner and a card's name are read from.</summary>
    public async Task<Account?> AccountAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        var now = _sessions.Clock.GetUtcNow();
        if (_cache.TryGetValue(id, out var cached) && now - cached.Read < CacheFor)
        {
            return cached.Account;
        }

        var account = await _registry.FindAsync(id, cancellationToken);
        if (account is null)
        {
            _cache.TryRemove(id, out _);
            return null;
        }

        _cache[id] = (account, now);
        return account;
    }

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, AccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return (method, path) switch
            {
                ("GET", Prefix) => await MeAsync(request),
                ("POST", Prefix) => await RegisterAsync(body, request),
                ("POST", Prefix + "/google") => await GoogleAsync(body, request),
                ("POST", Prefix + "/dev") => await DevelopmentAsync(body, request),
                ("POST", Prefix + "/signout") => await SignOutAsync(body, request),
                ("GET", _) when path.StartsWith(NamesPrefix, StringComparison.Ordinal) => await NameAsync(Uri.UnescapeDataString(path[NamesPrefix.Length..])),
                _ => Problem(404, "Account.NoRoute", "No such account route."),
            };
        }
        catch (JsonException)
        {
            return Problem(400, "Account.BadRequest", "The request is not valid JSON.");
        }
    }

    /// <summary>The account as a page is told it: its id and its name, and nothing else the host keeps.</summary>
    public static object Describe(Account account) => new { id = account.Id.Value, username = account.Username.Value };

    private async Task<StudioResponse> MeAsync(AccountRequest request)
    {
        var who = await WhoAsync(request);
        return Answer(new { account = who is null ? null : Describe(who.Account) }, who?.Renewed);
    }

    private async Task<StudioResponse> GoogleAsync(string body, AccountRequest request)
    {
        if (Mode != AccountsMode.Google)
        {
            return Problem(404, "Account.NoGoogle", "This host does not sign in with Google.");
        }

        var credential = Read<CredentialBody>(body)?.Credential;
        if (string.IsNullOrWhiteSpace(credential) || await _verifier!.VerifyAsync(credential) is not { } identity)
        {
            return Problem(401, "Account.SignInRejected", "Google's sign-in could not be checked. Sign in again.");
        }

        return await _registry.FindAsync(identity) is { } account
            ? SignedInAs(account, request)
            : Answer(new { needsUsername = true, ticket = _sessions.Ticket(identity) });
    }

    private async Task<StudioResponse> RegisterAsync(string body, AccountRequest request)
    {
        var asked = Read<RegisterBody>(body);
        if (_sessions.ReadTicket(asked?.Ticket) is not { } identity)
        {
            return Problem(401, "Account.TicketExpired", "That sign-in has run out. Sign in again, then choose your name.");
        }

        var registered = await _registry.RegisterAsync(identity, asked!.Username);
        return registered.IsSuccess ? SignedInAs(registered.Value, request) : Refused(registered.Error);
    }

    private async Task<StudioResponse> DevelopmentAsync(string body, AccountRequest request)
    {
        if (Mode != AccountsMode.Development)
        {
            return Problem(404, "Account.NoDevelopment", "This host signs nobody in by a typed name.");
        }

        var name = Username.Create(Read<RegisterBody>(body)?.Username);
        if (name.IsFailure)
        {
            return Refused(name.Error);
        }

        // The name is the identity: whoever types it again is that account again, which is what a laptop's
        // table and a test want, and why this mode is refused anywhere but the loopback address.
        var identity = new ExternalIdentity(ExternalIdentity.Development, name.Value.Key);
        if (await _registry.FindAsync(identity) is { } account)
        {
            return SignedInAs(account, request);
        }

        var registered = await _registry.RegisterAsync(identity, name.Value.Value);
        return registered.IsSuccess ? SignedInAs(registered.Value, request) : Refused(registered.Error);
    }

    private async Task<StudioResponse> SignOutAsync(string body, AccountRequest request)
    {
        if (Read<SignOutBody>(body)?.Everywhere == true && await WhoAsync(request) is { } who)
        {
            if (await _registry.SignOutEverywhereAsync(who.Account.Id) is { } raised)
            {
                _cache[raised.Id] = (raised, _sessions.Clock.GetUtcNow());
            }
        }

        return Answer(new { signedOut = true }, AccountSessions.ClearCookie(request.Secure));
    }

    private async Task<StudioResponse> NameAsync(string candidate)
    {
        var check = await _registry.CheckUsernameAsync(candidate);
        return check.IsSuccess
            ? Answer(new { available = true })
            : Answer(new { available = false, error = check.Error.Code, message = check.Error.Message });
    }

    private StudioResponse SignedInAs(Account account, AccountRequest request) =>
        Answer(new { account = Describe(account) }, AccountSessions.SetCookie(_sessions.Issue(account), request.Secure));

    private static StudioResponse Refused(DomainError error) => Problem(
        error == AccountErrors.UsernameTaken || error == AccountErrors.AlreadyRegistered ? 409 : 400,
        error.Code,
        error.Message);

    private static T? Read<T>(string body)
        where T : class => string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<T>(body, Web);

    private static StudioResponse Answer(object payload, string? cookie = null) =>
        StudioResponse.OfJson(payload, Web) with { Headers = cookie is null ? null : [new KeyValuePair<string, string>("Set-Cookie", cookie)] };

    private static StudioResponse Problem(int status, string code, string message) =>
        StudioResponse.OfJson(new { error = code, message }, Web, status);

    private sealed record CredentialBody(string? Credential);

    private sealed record RegisterBody(string? Ticket, string? Username);

    private sealed record SignOutBody(bool? Everywhere);
}
