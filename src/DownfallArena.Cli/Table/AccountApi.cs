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

    private const string PushRoute = Prefix + "/push";
    private const string PreferencesRoute = Prefix + "/preferences";

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

    /// <summary>
    /// What deleting an account does at the tables before the account goes: the lobby offers its seats to other
    /// players. Set by the host that holds the tables; none where there are none.
    /// </summary>
    public Func<AccountId, Task>? ReleaseSeats { get; set; }

    /// <summary>The host's clock, as the sessions read it: when the operator suspends an account, among others.</summary>
    public TimeProvider Clock => _sessions.Clock;

    /// <summary>The Google client id the page's sign-in button is drawn for; public, as Google's own pages show it.</summary>
    public string? GoogleClientId { get; }

    /// <summary>
    /// The devices an account can be told on (friends plan, stage 1), set by the host that keeps them; none on a
    /// host without a store. Forgotten with "sign out everywhere" and with the account.
    /// </summary>
    public AccountDevices? Devices { get; set; }

    /// <summary>What sends a notice to those devices; none when the host has no push keys, and then no device is registered either.</summary>
    public AccountNotices? Notices { get; set; }

    /// <summary>What deleting an account does to its friendships (ADR 0129): no link to it left on anybody's list. Set by the host that keeps them.</summary>
    public Func<AccountId, Task>? ForgetFriends { get; set; }

    /// <summary>Where each account's game settings are kept (ADR 0140); null on a host that keeps none.</summary>
    public IPreferenceStore? Preferences { get; set; }

    /// <summary>The host's settings (ADR 0177), whether a new player may make an account; none leaves registration open.</summary>
    public HostSettingsCache? Settings { get; set; }

    /// <summary>
    /// When each account last signed in and last used the site (ADR 0174), written as it happens; null on a host
    /// that keeps none. Forgotten with the account.
    /// </summary>
    public AccountActivityLog? Activity { get; set; }

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
            || account.Generation != claim.Generation
            || account.Suspension is not null)
        {
            return null;
        }

        var renewed = _sessions.DueForRenewal(claim)
            ? AccountSessions.SetCookie(_sessions.Issue(account), request.Secure)
            : null;
        if (Activity is { } activity)
        {
            await activity.SeenAsync(account.Id, cancellationToken);
        }

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

    /// <summary>
    /// Raises the account's generation, so every session it holds stops working, and forgets its devices: a phone
    /// that signed out everywhere was lost or lent, and must be told nothing more. Its player's own "sign out
    /// everywhere" and the operator's (ADR 0174) alike. Null when there is no such account.
    /// </summary>
    public async Task<Account?> SignOutEverywhereAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        var raised = await _registry.SignOutEverywhereAsync(id, cancellationToken);
        if (raised is not null)
        {
            _cache[raised.Id] = (raised, _sessions.Clock.GetUtcNow());
            if (Devices is { } devices)
            {
                await devices.ForgetAsync(id, cancellationToken);
            }
        }

        return raised;
    }

    /// <summary>
    /// Deletes the account and everything kept for it (ADR 0122, ADR 0174): its seats in unfinished games are offered
    /// to other players, the name is freed, every session of it stops working, and its devices, friendships, settings
    /// and activity go. Its player's own deletion and the operator's alike. False when there is no such account.
    /// </summary>
    public async Task<bool> ForgetAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        if (await _registry.FindAsync(id, cancellationToken) is null)
        {
            return false;
        }

        if (ReleaseSeats is { } release)
        {
            await release(id);
        }

        // The account goes first, so a request that resolved its name a moment ago finds nobody once it reaches the
        // friendship lock; what the account was still on is cleaned up after, under that same lock.
        var deleted = await _registry.DeleteAsync(id, cancellationToken);
        _cache.TryRemove(id, out _);
        if (Devices is { } devices)
        {
            await devices.ForgetAsync(id, cancellationToken);
        }

        if (ForgetFriends is { } forget)
        {
            await forget(id);
        }

        if (Preferences is { } preferences)
        {
            await preferences.ForgetAsync(id, cancellationToken);
        }

        if (Activity is { } activity)
        {
            await activity.ForgetAsync(id, cancellationToken);
        }

        return deleted;
    }

    /// <summary>Gives the account another name (ADR 0174), checked as a chosen name is; the name it had is freed.</summary>
    public async Task<Result<Account>> RenameAsync(AccountId id, string? username, CancellationToken cancellationToken = default)
    {
        var renamed = await _registry.RenameAsync(id, username, cancellationToken);
        if (renamed.IsSuccess)
        {
            _cache[id] = (renamed.Value, _sessions.Clock.GetUtcNow());
        }

        return renamed;
    }

    /// <summary>
    /// Suspends the account (ADR 0178): every session it holds stops working and its devices are forgotten, as with
    /// "sign out everywhere", and no new session is issued until it is restored.
    /// </summary>
    public async Task<Result<Account>> SuspendAsync(AccountId id, AccountSuspension suspension, CancellationToken cancellationToken = default)
    {
        var suspended = await _registry.SuspendAsync(id, suspension, cancellationToken);
        if (suspended.IsSuccess)
        {
            _cache[id] = (suspended.Value, _sessions.Clock.GetUtcNow());
            if (Devices is { } devices)
            {
                await devices.ForgetAsync(id, cancellationToken);
            }
        }

        return suspended;
    }

    /// <summary>Lifts the account's suspension (ADR 0178): its player signs in again.</summary>
    public async Task<Result<Account>> RestoreAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        var restored = await _registry.RestoreAsync(id, cancellationToken);
        if (restored.IsSuccess)
        {
            _cache[id] = (restored.Value, _sessions.Clock.GetUtcNow());
        }

        return restored;
    }

    /// <summary>The account as the store holds it now, the cache refreshed with it: what the operator acts on.</summary>
    public async Task<Account?> FreshAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(id, out _);
        return await AccountAsync(id, cancellationToken);
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
                ("DELETE", Prefix) => await DeleteAsync(body, request),
                ("PUT", PushRoute) => await RegisterDeviceAsync(body, request),
                ("DELETE", PushRoute) => await ForgetDeviceAsync(body, request),
                ("POST", PushRoute + "/test") => await TestDeviceAsync(body, request),
                ("GET", PreferencesRoute) => await PreferencesAsync(request),
                ("PUT", PreferencesRoute) => await SavePreferencesAsync(body, request),
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

        if (await _registry.FindAsync(identity) is { } account)
        {
            return await SignedInAsync(account, request);
        }

        // Closed means closed (ADR 0177): no name is asked of a sign-in that has no account.
        return await RegistrationClosedAsync()
            ? Refused(AccountErrors.RegistrationClosed)
            : Answer(new { needsUsername = true, ticket = _sessions.Ticket(identity) });
    }

    /// <summary>Whether the operator has new accounts closed (ADR 0177); a host without settings keeps them open.</summary>
    private async Task<bool> RegistrationClosedAsync() => Settings is { } settings && !(await settings.CurrentAsync()).RegistrationOpen;

    private async Task<StudioResponse> RegisterAsync(string body, AccountRequest request)
    {
        var asked = Read<RegisterBody>(body);
        if (_sessions.ReadTicket(asked?.Ticket) is not { } identity)
        {
            return Problem(401, "Account.TicketExpired", "That sign-in has run out. Sign in again, then choose your name.");
        }

        // A ticket handed out before the operator closed registration is refused too (ADR 0177).
        if (await RegistrationClosedAsync())
        {
            return Refused(AccountErrors.RegistrationClosed);
        }

        var registered = await _registry.RegisterAsync(identity, asked!.Username);
        return registered.IsSuccess ? await SignedInAsync(registered.Value, request) : Refused(registered.Error);
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
            return await SignedInAsync(account, request);
        }

        var registered = await _registry.RegisterAsync(identity, name.Value.Value);
        return registered.IsSuccess ? await SignedInAsync(registered.Value, request) : Refused(registered.Error);
    }

    private async Task<StudioResponse> SignOutAsync(string body, AccountRequest request)
    {
        if (Read<SignOutBody>(body)?.Everywhere == true && await WhoAsync(request) is { } who)
        {
            await SignOutEverywhereAsync(who.Account.Id);
        }

        return Answer(new { signedOut = true }, AccountSessions.ClearCookie(request.Secure));
    }

    /// <summary>
    /// Deletes the signed-in account (ADR 0122), once its player has typed their name to confirm: its seats in
    /// unfinished games are offered to other players, the name is freed, and every session of it stops working.
    /// </summary>
    private async Task<StudioResponse> DeleteAsync(string body, AccountRequest request)
    {
        if (await WhoAsync(request) is not { } who)
        {
            return Problem(401, "Account.SignInRequired", "Sign in to delete your account.");
        }

        if (!string.Equals(Read<RegisterBody>(body)?.Username?.Trim(), who.Account.Username.Value, StringComparison.OrdinalIgnoreCase))
        {
            return Problem(400, "Account.ConfirmDelete", "Type your player name exactly to delete your account.");
        }

        await ForgetAsync(who.Account.Id);
        return Answer(new { deleted = true }, AccountSessions.ClearCookie(request.Secure));
    }

    /// <summary>
    /// This browser is one of the signed-in account's devices from now on (friends plan, stage 1), as the browser's
    /// <c>PushSubscription.toJSON()</c> gives it. <c>410</c> for a subscription a push service already said was gone:
    /// the browser still holds it and sends it on every load, and that answer is how the page learns to make a new one.
    /// </summary>
    private async Task<StudioResponse> RegisterDeviceAsync(string body, AccountRequest request)
    {
        if (Notices is not { } notices || Devices is not { } devices)
        {
            return Problem(404, "Account.NoNotices", "This host sends no notices: it has no push keys.");
        }

        if (await WhoAsync(request) is not { } who)
        {
            return Problem(401, "Account.SignInRequired", "Sign in to be notified on this device.");
        }

        var sent = Read<BrowserSubscription>(body);
        var subscription = PushSubscription.From(sent?.Endpoint, sent?.Keys?.P256dh, sent?.Keys?.Auth);
        if (subscription is null)
        {
            return Problem(400, "Account.BadSubscription", "Send the subscription as the browser serialises it: an https endpoint, a p256dh key of 65 bytes and an auth secret of 16.");
        }

        if (notices.IsDead(subscription.Endpoint))
        {
            return Problem(410, "Account.DeviceGone", "This subscription is gone from its push service. Subscribe again.");
        }

        var held = await devices.AddAsync(who.Account.Id, new PushDevice(subscription.Endpoint, subscription.P256dh, subscription.Auth, default));
        return Answer(new { notices = true, devices = held.Count }, who.Renewed);
    }

    /// <summary>This browser is told nothing more; the account's other devices are left as they are.</summary>
    private async Task<StudioResponse> ForgetDeviceAsync(string body, AccountRequest request)
    {
        if (Devices is not { } devices)
        {
            return Problem(404, "Account.NoNotices", "This host sends no notices: it has no push keys.");
        }

        if (await WhoAsync(request) is not { } who)
        {
            return Problem(401, "Account.SignInRequired", "Sign in first.");
        }

        if (Read<BrowserSubscription>(body)?.Endpoint is not { Length: > 0 } endpoint)
        {
            return Problem(400, "Account.BadSubscription", "Say which device, by its endpoint.");
        }

        await devices.RemoveAsync(who.Account.Id, endpoint);
        return Answer(new { notices = false }, who.Renewed);
    }

    /// <summary>A notice to this device now, and what came of it: how a player sees that the browser can be reached.</summary>
    private async Task<StudioResponse> TestDeviceAsync(string body, AccountRequest request)
    {
        if (Notices is not { } notices)
        {
            return Problem(404, "Account.NoNotices", "This host sends no notices: it has no push keys.");
        }

        if (await WhoAsync(request) is not { } who)
        {
            return Problem(401, "Account.SignInRequired", "Sign in first.");
        }

        var test = await notices.TestAsync(who.Account.Id, Read<BrowserSubscription>(body)?.Endpoint);
        return StudioResponse.OfJson(new { sent = test.Sent, message = test.Message }, Web, test.Sent ? 200 : 502);
    }

    /// <summary>The settings every new game of the account starts from (ADR 0140): the saved ones, or the defaults.</summary>
    private async Task<StudioResponse> PreferencesAsync(AccountRequest request)
    {
        if (Preferences is not { } store)
        {
            return Problem(404, "Account.NoPreferences", "This host keeps no game settings.");
        }

        if (await WhoAsync(request) is not { } who)
        {
            return Problem(401, "Account.SignInRequired", "Sign in first.");
        }

        var saved = await store.GetAsync(who.Account.Id);
        return Answer(new { preferences = DescribePreferences(saved ?? GamePreferences.Default), saved = saved is not null }, who.Renewed);
    }

    /// <summary>Replaces the account's settings; a game already open keeps what it was set to.</summary>
    private async Task<StudioResponse> SavePreferencesAsync(string body, AccountRequest request)
    {
        if (Preferences is not { } store)
        {
            return Problem(404, "Account.NoPreferences", "This host keeps no game settings.");
        }

        if (await WhoAsync(request) is not { } who)
        {
            return Problem(401, "Account.SignInRequired", "Sign in first.");
        }

        var asked = Read<PreferencesBody>(body);
        var created = GamePreferences.Create(asked?.Pace, asked?.Speed ?? 0m, asked?.Help ?? true, asked?.ReducedMotion ?? false, asked?.Reactions ?? true);
        if (created.IsFailure)
        {
            return Refused(created.Error);
        }

        await store.SaveAsync(who.Account.Id, created.Value);
        return Answer(new { preferences = DescribePreferences(created.Value), saved = true }, who.Renewed);
    }

    private static object DescribePreferences(GamePreferences preferences) => new
    {
        pace = preferences.Pace == ReadingPace.Step ? "step" : "auto",
        speed = preferences.Speed,
        help = preferences.Help,
        reducedMotion = preferences.ReducedMotion,
        reactions = preferences.Reactions,
    };

    private async Task<StudioResponse> NameAsync(string candidate)
    {
        var check = await _registry.CheckUsernameAsync(candidate);
        return check.IsSuccess
            ? Answer(new { available = true })
            : Answer(new { available = false, error = check.Error.Code, message = check.Error.Message });
    }

    private async Task<StudioResponse> SignedInAsync(Account account, AccountRequest request)
    {
        // A suspended account is told why, and given no session (ADR 0178).
        if (account.Suspension is { } suspension)
        {
            var refused = AccountErrors.Suspended(suspension);
            return Problem(403, refused.Code, refused.Message);
        }

        if (Activity is { } activity)
        {
            await activity.SignedInAsync(account.Id);
        }

        return Answer(new { account = Describe(account) }, AccountSessions.SetCookie(_sessions.Issue(account), request.Secure));
    }

    private static StudioResponse Refused(DomainError error) => Problem(
        error == AccountErrors.UsernameTaken || error == AccountErrors.AlreadyRegistered || error == AccountErrors.RegistrationClosed ? 409 : 400,
        error.Code,
        error.Message);

    private static T? Read<T>(string body)
        where T : class => string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<T>(body, Web);

    private static StudioResponse Answer(object payload, string? cookie = null) =>
        StudioResponse.OfJson(payload, Web) with { Headers = cookie is null ? null : [new KeyValuePair<string, string>("Set-Cookie", cookie)] };

    private static StudioResponse Problem(int status, string code, string message) =>
        StudioResponse.OfJson(new { error = code, message }, Web, status);

    private sealed record CredentialBody(string? Credential);

    private sealed record PreferencesBody(string? Pace, decimal? Speed, bool? Help, bool? ReducedMotion, bool? Reactions);

    private sealed record RegisterBody(string? Ticket, string? Username);

    private sealed record SignOutBody(bool? Everywhere);

    private sealed record BrowserSubscription(string? Endpoint, BrowserKeys? Keys);

    private sealed record BrowserKeys(string? P256dh, string? Auth);
}
