using System.Collections.Concurrent;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Hosting;
using DownfallArena.Cli.Studio;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Signing in and registering (ADR 0122): a provider's proof in at <c>POST /api/account/&lt;provider&gt;</c> (Google's
/// ID token, for one), the host's own session cookie out, and a name chosen in between when the sign-in has no
/// account yet. Each provider <c>--sign-in</c> lists is one <see cref="ISignInProvider" /> (sign-in methods plan, P2).
/// <c>--accounts dev</c> skips them all and signs in by a typed name, on a loopback host only. Every route is behind the table's same-origin fence, which is what makes a cookie safe
/// to act on: another site cannot send this host a JSON write.
/// </summary>
internal sealed class AccountApi
{
    public const string Prefix = "/api/account";

    private const string NamesPrefix = Prefix + "/names/";

    private const string PushRoute = Prefix + "/push";
    private const string PreferencesRoute = Prefix + "/preferences";
    private const string NoticePreferencesRoute = PreferencesRoute + "/notices";
    private const string NoticesRoute = Prefix + "/notices";
    private const string IdentitiesRoute = Prefix + "/identities";

    /// <summary>Where a visitor is made a guest (ADR 0207).</summary>
    public const string GuestRoute = Prefix + "/guest";

    // What a route that needs a signed-in account answers to nobody.
    private const string SignInRequiredCode = "Account.SignInRequired";
    private const string SignInFirst = "Sign in first.";

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
    private readonly Dictionary<string, ISignInProvider> _providers;

    public AccountApi(AccountRegistry registry, AccountSessions sessions, AccountsMode mode, IReadOnlyList<ISignInProvider>? providers = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(sessions);
        if (mode == AccountsMode.Off)
        {
            throw new ArgumentException("A host with accounts off has no account API.", nameof(mode));
        }

        providers ??= [];
        if (mode == AccountsMode.SignIn && providers.Count == 0)
        {
            throw new ArgumentException("A host that signs in with providers needs at least one.", nameof(providers));
        }

        if (mode != AccountsMode.SignIn && providers.Count > 0)
        {
            throw new ArgumentException("Only a host that signs in with providers is given any.", nameof(providers));
        }

        if (providers.FirstOrDefault(provider => string.IsNullOrEmpty(provider.Name) || !provider.Name.All(char.IsAsciiLetterLower)) is { } misnamed)
        {
            throw new ArgumentException($"A provider is named in lower-case letters, as its route is: not '{misnamed.Name}'.", nameof(providers));
        }

        if (providers.DistinctBy(provider => provider.Name, StringComparer.Ordinal).Count() != providers.Count)
        {
            throw new ArgumentException("Each provider signs in at its own route: one of them is given twice.", nameof(providers));
        }

        _registry = registry;
        _sessions = sessions;
        _providers = providers.ToDictionary(provider => provider.Name, StringComparer.Ordinal);
        SignIn = [.. providers];
        Mode = mode;
    }

    public AccountsMode Mode { get; }

    /// <summary>
    /// What deleting an account does at the tables before the account goes: the lobby offers its seats to other
    /// players. Set by the host that holds the tables; none where there are none.
    /// </summary>
    public Func<AccountId, Task>? ReleaseSeats { get; set; }

    /// <summary>The host's clock, as the sessions read it: when the operator suspends an account, among others.</summary>
    public TimeProvider Clock => _sessions.Clock;

    /// <summary>The providers this host signs in with, in the order the page draws their buttons; none in the other modes.</summary>
    public IReadOnlyList<ISignInProvider> SignIn { get; }

    /// <summary>
    /// The providers as <c>/api/me</c> tells them to a page: each one's name and the public id its button is drawn for,
    /// in order. Nothing secret: a client id is on the provider's own pages.
    /// </summary>
    public object[] Offered() => [.. SignIn.Select(provider => new { provider = provider.Name, clientId = provider.ClientId })];

    /// <summary>
    /// Where the sign-ins' counts go, one line a minute per provider (<see cref="SignInCounts" />, #1171): started when a
    /// provider's proof reaches the host, then finished when a session is issued, or refused, unavailable, or limited
    /// for its address's count. And why a provider is unavailable, once a minute. The console by default, which the ops
    /// report's logs keep; a line names the provider and nothing about the person.
    /// </summary>
    public Action<string> Said { get; set; } = Console.WriteLine;

    private readonly SignInCounts _counts = new();
    private readonly SignInAttempts _attempts = new();
    private readonly GuestMaking _guests = new();

    /// <summary>
    /// Says the sign-ins' counts once their minute is over. Run on every request the account API answers, and by the
    /// host on every other one, so a minute's counts wait for no sign-in to be said.
    /// </summary>
    public void SayCountsIfDue()
    {
        foreach (var line in _counts.Due(Clock.GetUtcNow()))
        {
            Said(line);
        }
    }

    /// <summary>Says every count held, its minute over or not: the host stops, and would otherwise take them with it.</summary>
    public void SayCounts()
    {
        foreach (var line in _counts.All())
        {
            Said(line);
        }
    }

    /// <summary>
    /// The devices an account can be told on (friends plan, stage 1), set by the host that keeps them; none on a
    /// host without a store. Forgotten with "sign out everywhere" and with the account.
    /// </summary>
    public AccountDevices? Devices { get; set; }

    /// <summary>What sends a notice to those devices; none when the host has no push keys, and then no device is registered either.</summary>
    public AccountNotices? Notices { get; set; }

    /// <summary>
    /// The account's notice log (ADR 0201): the news its bell keeps whatever the push does. Set by the host that keeps
    /// the accounts, push keys or not; forgotten with the account.
    /// </summary>
    public AccountNoticeLog? NoticeLog { get; set; }

    /// <summary>What tells the account's other pages its notices were read, so their bells drop the count (ADR 0164); none where nobody listens.</summary>
    public LobbyChanges? Changes { get; set; }

    /// <summary>What deleting an account does to its friendships (ADR 0129): no link to it left on anybody's list. Set by the host that keeps them.</summary>
    public Func<AccountId, Task>? ForgetFriends { get; set; }

    /// <summary>What deleting an account does to its feedback (ADR 0189): its text is kept, the account dropped. Set by the host that keeps it.</summary>
    public Func<AccountId, Task>? ForgetFeedback { get; set; }

    /// <summary>Where each account's game settings are kept (ADR 0140); null on a host that keeps none.</summary>
    public IPreferenceStore? Preferences { get; set; }

    // One writer at a time reads the Tutorials an account keeps, changes them and writes them back, each after
    // the one before for that account: the store replaces a document whole, and one replica is the most the
    // table runs. An account's line is let go of once its last change is through.
    private readonly Lock _noting = new();
    private readonly Dictionary<AccountId, Task> _noted = [];

    /// <summary>The Tutorials the account finished and skipped (ADR 0187); null on a host that keeps no game settings.</summary>
    public async Task<TutorialProgress?> TutorialsAsync(AccountId account, CancellationToken cancellationToken = default) =>
        Preferences is { } store ? await store.GetTutorialsAsync(account, cancellationToken) : null;

    /// <summary>
    /// Keeps that the account finished or skipped a Tutorial, and answers what it keeps now; null on a host that
    /// keeps no game settings, and for an account that is gone. Said twice, it is kept once; a Tutorial finished is never skipped again.
    /// </summary>
    public async Task<TutorialProgress?> NoteTutorialAsync(AccountId account, string tutorial, TutorialState state, CancellationToken cancellationToken = default)
    {
        if (Preferences is not { } store)
        {
            return null;
        }

        return await AfterTheLastAsync<TutorialProgress?>(account, async () =>
        {
            // A request that signed in a moment before its account was deleted reaches this after the deletion
            // forgot the account's documents: it must not write one back.
            if (await _registry.FindAsync(account, cancellationToken) is null)
            {
                return null;
            }

            var kept = await store.GetTutorialsAsync(account, cancellationToken);
            var next = state switch
            {
                TutorialState.Done => kept.WithDone(tutorial),
                TutorialState.Skipped => kept.WithSkipped(tutorial),
                _ => kept,
            };
            if (next != kept)
            {
                await store.SaveTutorialsAsync(account, next, cancellationToken);
            }

            return next;
        });
    }

    /// <summary>Runs a change to what the preference store keeps of an account after the one before it for that account, whoever asked for that one.</summary>
    private async Task<T> AfterTheLastAsync<T>(AccountId account, Func<Task<T>> change)
    {
        Task<T> changing;
        lock (_noting)
        {
            changing = AfterAsync(_noted.GetValueOrDefault(account, Task.CompletedTask), change);
            _noted[account] = changing;
        }

        try
        {
            return await changing;
        }
        finally
        {
            lock (_noting)
            {
                if (_noted.GetValueOrDefault(account) == changing)
                {
                    _noted.Remove(account);
                }
            }
        }
    }

    private static async Task<T> AfterAsync<T>(Task before, Func<Task<T>> change)
    {
        // The change before this one may have failed: that is its caller's to hear, not this one's.
        await before.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        return await change();
    }

    /// <summary>The host's settings (ADR 0177), whether a new player may make an account; none leaves registration open.</summary>
    public HostSettingsCache? Settings { get; set; }

    /// <summary>
    /// When each account last signed in and last used the site (ADR 0174), written as it happens; null on a host
    /// that keeps none. Forgotten with the account.
    /// </summary>
    public AccountActivityLog? Activity { get; set; }

    /// <summary>
    /// How <c>/api/me</c> names the mode to a page; which providers sign in is its <c>signIn</c> list. A page tells off and
    /// dev apart and reads anything else as signing in through the providers listed (sign-in methods plan, P3).
    /// </summary>
    public static string Named(AccountsMode mode) => mode switch
    {
        AccountsMode.SignIn => "signin",
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

        // Its feedback loses the account before the account goes (ADR 0189): a store that fails here leaves the
        // account in place, and its player can delete it again, rather than feedback naming an account that is gone.
        if (ForgetFeedback is { } feedback)
        {
            await feedback(id);
        }

        // The account goes first, so a request that resolved its name a moment ago finds nobody once it reaches the
        // friendship lock; what the account was still on is cleaned up after, under that same lock.
        var deleted = await _registry.DeleteAsync(id, cancellationToken);
        _cache.TryRemove(id, out _);
        if (Devices is { } devices)
        {
            await devices.ForgetAsync(id, cancellationToken);
        }

        if (NoticeLog is { } log)
        {
            // After the account: a notice on its way reads the account gone and forgets what it wrote. A log that
            // cannot be forgotten now does not stop the rest of the deletion, which a retry could not finish.
            try
            {
                await log.ForgetAsync(id, cancellationToken);
            }
            catch (Exception failure) when (TableRegistry.StoreFails(failure))
            {
                Console.WriteLine($"  Account {id}: its notices could not be forgotten with it: {HttpHost.OneLine(failure)}");
            }
        }

        if (ForgetFriends is { } forget)
        {
            await forget(id);
        }

        if (Preferences is { } preferences)
        {
            // After any Tutorial being noted for the account: a note that read before the account went must not
            // write its document back once it is gone.
            await AfterTheLastAsync(id, async () =>
            {
                await preferences.ForgetAsync(id, cancellationToken);
                return true;
            });
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
        SayCountsIfDue();
        try
        {
            return (method, path) switch
            {
                ("GET", Prefix) => await MeAsync(request),
                ("POST", Prefix) => await RegisterAsync(body, request),
                ("POST", Prefix + "/dev") => await DevelopmentAsync(body, request),
                ("POST", Prefix + "/signout") => await SignOutAsync(body, request),
                ("POST", GuestRoute) => await MakeGuestAsync(request),
                ("DELETE", Prefix) => await DeleteAsync(body, request),
                ("PUT", PushRoute) => await RegisterDeviceAsync(body, request),
                ("DELETE", PushRoute) => await ForgetDeviceAsync(body, request),
                ("POST", PushRoute + "/test") => await TestDeviceAsync(body, request),
                ("GET", NoticesRoute) => await NoticesAsync(request),
                ("POST", NoticesRoute + "/read") => await ReadNoticesAsync(body, request),
                ("GET", PreferencesRoute) => await PreferencesAsync(request),
                ("PUT", PreferencesRoute) => await SavePreferencesAsync(body, request),
                ("PUT", NoticePreferencesRoute) => await SaveNoticePreferencesAsync(body, request),
                ("GET", IdentitiesRoute) => await IdentitiesAsync(request),
                ("POST", _) when WayOf(path) is { } added => await AddWayAsync(added, body, request),
                ("DELETE", _) when WayOf(path) is { } removed => await RemoveWayAsync(removed, body, request),
                ("GET", _) when path.StartsWith(NamesPrefix, StringComparison.Ordinal) => await NameAsync(Uri.UnescapeDataString(path[NamesPrefix.Length..])),
                ("POST", _) when ProviderOf(path) is { } provider => await ProviderAsync(provider, body, request),
                _ => Problem(404, "Account.NoRoute", "No such account route."),
            };
        }
        catch (JsonException)
        {
            return Problem(400, "Account.BadRequest", "The request is not valid JSON.");
        }
    }

    /// <summary>The account as a page is told it: its id and its name, and nothing else the host keeps.</summary>
    public static object Describe(Account account) => new { id = account.Id.Value, username = account.Username.Value, guest = account.IsGuest };

    /// <summary>
    /// The guest a session cookie proves (ADR 0207), or null: no session, one this host does not honour, or an account
    /// that is not a guest. Read from the cache as <see cref="WhoAsync" /> reads, and writes nothing: the host asks it of
    /// every request that carries a session, before the route that answers it.
    /// </summary>
    public async Task<Account?> GuestAsync(AccountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _sessions.Read(request.Session) is { } claim
            && await AccountAsync(claim.Account, cancellationToken) is { IsGuest: true } account
            && account.Generation == claim.Generation
            && account.Suspension is null
                ? account
                : null;
    }

    /// <summary>
    /// Makes the visitor a guest (ADR 0207) and signs it in: the same session cookie as any account's. A visitor already
    /// signed in is answered the account it has, guest or not, and no other is made. Refused while registration is closed
    /// (ADR 0177), and past the address's or the host's count of guests an hour.
    /// </summary>
    private async Task<StudioResponse> MakeGuestAsync(AccountRequest request)
    {
        if (await WhoAsync(request) is { } who)
        {
            return Answer(new { account = Describe(who.Account) }, who.Renewed);
        }

        if (await RegistrationClosedAsync())
        {
            return Refused(AccountErrors.RegistrationClosed);
        }

        if (_guests.Take(request.Address, Clock.GetUtcNow()) is { } wait)
        {
            var seconds = Math.Max(1, (long)Math.Ceiling(wait.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Problem(429, "Account.TooManyGuests", "Too many guests from your network just now. Try again later, or sign in.") with
            {
                Headers = [new KeyValuePair<string, string>("Retry-After", seconds)],
            };
        }

        Counted(SignInCounts.Step.Started, ExternalIdentity.Guest);
        Result<Account> made;
        try
        {
            made = await _registry.MakeGuestAsync();
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure))
        {
            Counted(SignInCounts.Step.Unavailable, ExternalIdentity.Guest);
            Said($"  Guest not made: {HttpHost.OneLine(failure)}");
            return AccountsUnavailable();
        }

        if (made.IsFailure)
        {
            Counted(SignInCounts.Step.Refused, ExternalIdentity.Guest);
            return Refused(made.Error);
        }

        Said($"  Guest made: account {made.Value.Id}.");
        return await SignedInAsync(made.Value, request, ExternalIdentity.Guest);
    }

    private async Task<StudioResponse> MeAsync(AccountRequest request)
    {
        var who = await WhoAsync(request);
        return Answer(new { account = who is null ? null : Describe(who.Account) }, who?.Renewed);
    }

    /// <summary>The one path segment after the prefix, as a provider names itself; null for any other path.</summary>
    private static string? ProviderOf(string path) =>
        path.Length > Prefix.Length + 1 && path.StartsWith(Prefix + "/", StringComparison.Ordinal) && path[(Prefix.Length + 1)..] is var name && name.All(char.IsAsciiLetterLower)
            ? name
            : null;

    private async Task<StudioResponse> ProviderAsync(string name, string body, AccountRequest request)
    {
        if (!_providers.TryGetValue(name, out var provider))
        {
            return Problem(404, "Account.NoProvider", "This host does not sign in that way.");
        }

        var (identity, refusal) = await VouchedAsync(provider, body, request, counted: true);
        if (refusal is not null)
        {
            return refusal;
        }

        if (await _registry.FindAsync(identity) is { } account)
        {
            var guest = await GuestAsync(request);
            var signedIn = await SignedInAsync(account, request, name);
            // Only a guest other than the account signed in, and still a guest in the store: one upgraded meanwhile into
            // that very account, while this process still held it as a guest, is that account now.
            if (guest is not null && guest.Id != account.Id && signedIn.Status == 200)
            {
                // The guest's cookie is replaced by the account's: nothing reaches the guest again (ADR 0207). A store
                // that fails leaves it to the hourly job, and the player signed in all the same.
                try
                {
                    if (await FreshAsync(guest.Id) is { IsGuest: true })
                    {
                        await ForgetAsync(guest.Id);
                        Said($"  Guest left for an account of its own ({name}): account {guest.Id} removed.");
                    }
                }
                catch (Exception failure) when (TableRegistry.StoreFails(failure))
                {
                    Said($"  Guest left for an account of its own ({name}): account {guest.Id} could not be removed: {HttpHost.OneLine(failure)}");
                }
            }

            return signedIn;
        }

        // Closed means closed (ADR 0177): no name is asked of a sign-in that has no account.
        if (await RegistrationClosedAsync())
        {
            Counted(SignInCounts.Step.Refused, name);
            return Refused(AccountErrors.RegistrationClosed);
        }

        return Answer(new { needsUsername = true, ticket = _sessions.Ticket(identity) });
    }

    /// <summary>
    /// The identity the provider vouches for from what the page posted, or the answer that refuses it: a body that is
    /// not one object, an address past its count (#1193), a proof the provider refuses or answers for another provider,
    /// a provider out of reach.
    /// <paramref name="counted" /> says the sign-in's steps, which only a sign-in counts.
    /// </summary>
    private async Task<(ExternalIdentity Identity, StudioResponse? Refusal)> VouchedAsync(ISignInProvider provider, string body, AccountRequest request, bool counted)
    {
        var name = provider.Name;
        var proof = Proof(body);
        if (proof is null)
        {
            return (default, Problem(400, "Account.BadRequest", "A sign-in is posted as one JSON object."));
        }

        // Checked by address before the provider is asked (#1193): its budget is every player's, not one address's.
        if (request.Address is { } address && _attempts.Admit(address, Clock.GetUtcNow()) is { } wait)
        {
            return (default, Limited(name, wait));
        }

        // Counted only once the provider answered: a provider that fails unexpectedly cost the address nothing.
        var outcome = SignInAttempts.Outcome.Unchecked;
        ExternalIdentity? vouched = null;
        try
        {
            if (counted)
            {
                Counted(SignInCounts.Step.Started, name);
            }

            vouched = await provider.SignInAsync(proof);
            outcome = SignInAttempts.Outcome.Refused;
        }
        catch (SignInUnavailableException exception)
        {
            // A provider that answered spent its budget: the address pays for it, whatever the player is told.
            if (exception.ProviderAnswered)
            {
                outcome = SignInAttempts.Outcome.Refused;
            }

            // Not the player's fault, and not a refusal: counted apart, so an outage does not read as players turned away.
            // Why is said once a minute: every post would otherwise write it again.
            if (Counted(SignInCounts.Step.Unavailable, name) == 1)
            {
                Said($"  Sign-in unavailable ({name}): {exception.Message}");
            }

            return (default, Problem(503, "Account.ProviderUnavailable", "That sign-in cannot be checked right now. Try again in a minute."));
        }
        finally
        {
            Settle(request, name, outcome, vouched);
        }

        if (vouched is not { } identity || identity.Provider != name)
        {
            if (counted)
            {
                Counted(SignInCounts.Step.Refused, name);
            }

            return (default, Problem(401, "Account.SignInRejected", "Your sign-in could not be checked. Sign in again."));
        }

        return (identity, null);
    }

    /// <summary>The provider a ways-to-sign-in path names after <c>/identities/</c>, in lower-case letters; null for any other path.</summary>
    private static string? WayOf(string path) =>
        path.StartsWith(IdentitiesRoute + "/", StringComparison.Ordinal) && path[(IdentitiesRoute.Length + 1)..] is { Length: > 0 } name && name.All(char.IsAsciiLetterLower)
            ? name
            : null;

    /// <summary>
    /// The ways into an account as a page is told them: each one's provider, in the order added, and whether the host
    /// would remove it; never its identifier. The page draws Remove where the host says so.
    /// </summary>
    private static object Ways(Account account) => new
    {
        identities = account.Identities.All.Select(identity => new { provider = identity.Provider, removable = account.Identities.Without(identity).IsSuccess }).ToArray(),
    };

    /// <summary>Settings' ways to sign in (sign-in methods plan, P4): the providers the signed-in account uses.</summary>
    private async Task<StudioResponse> IdentitiesAsync(AccountRequest request)
    {
        if (await WhoAsync(request) is not { } who)
        {
            return SignInRequired("Sign in to see how you sign in.");
        }

        return Answer(Ways(who.Account), who.Renewed);
    }

    /// <summary>
    /// Adds a way into the signed-in account (ADR 0204): the identity is the one the provider vouches for from the
    /// proof posted now, never one the request names. One per provider; one another account uses is refused.
    /// Sessions are left alone. Asks for a recent sign-in (<see cref="RecentlySignedIn" />).
    /// </summary>
    private async Task<StudioResponse> AddWayAsync(string name, string body, AccountRequest request)
    {
        if (await WhoAsync(request) is not { } who)
        {
            return SignInRequired("Sign in to add a way to sign in.");
        }

        if (!_providers.TryGetValue(name, out var provider))
        {
            return Problem(404, "Account.NoProvider", "This host does not sign in that way.");
        }

        if (RecentlySignedIn(who.Account, body) is { } stale)
        {
            return stale;
        }

        var (identity, refusal) = await VouchedAsync(provider, body, request, counted: false);
        if (refusal is not null)
        {
            // The account is named, never the provider's identifier: a takeover is traced by the account it reached.
            Said($"  Way in refused ({name}) for account {who.Account.Id}: {refusal.Status}.");
            return refusal;
        }

        Result<Account> linked;
        try
        {
            linked = await _registry.LinkAsync(who.Account.Id, identity, who.Account.Generation);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure))
        {
            // The failure's type alone: a file store's message names the by-subject path, the provider's identifier.
            Said($"  Way in not added ({name}) to account {who.Account.Id}: {failure.GetType().Name}.");
            return WaysUnavailable();
        }

        if (linked.IsFailure)
        {
            Said($"  Way in refused ({name}) for account {who.Account.Id}: {linked.Error.Code}.");
            return WayRefused(linked.Error);
        }

        _cache[linked.Value.Id] = (linked.Value, _sessions.Clock.GetUtcNow());
        Said($"  Way in added ({name}) to account {linked.Value.Id}.");
        return Answer(Ways(linked.Value), who.Renewed);
    }

    /// <summary>
    /// Removes a way into the signed-in account (ADR 0204), whether or not the host still offers its provider; never
    /// the last. Every session of the account ends with it, and its devices are forgotten, as with "sign out
    /// everywhere": whoever came in through it is out. The browser that asked is handed a new session, even when
    /// forgetting the devices fails after the removal. Asks for a recent sign-in.
    /// </summary>
    private async Task<StudioResponse> RemoveWayAsync(string name, string body, AccountRequest request)
    {
        if (await WhoAsync(request) is not { } who)
        {
            return SignInRequired("Sign in to remove a way to sign in.");
        }

        if (RecentlySignedIn(who.Account, body) is { } stale)
        {
            return stale;
        }

        // A removal that stopped before its binding went keeps the identity apart (#1216): removing it again finishes it.
        if ((who.Account.Identities.ByProvider(name) ?? (who.Account.Unbinding is { } pending && pending.Provider == name ? pending : null)) is not { } identity)
        {
            return WayRefused(AccountErrors.IdentityNotLinked);
        }

        Result<Account> unlinked;
        try
        {
            unlinked = await _registry.UnlinkAsync(who.Account.Id, identity, who.Account.Generation);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure))
        {
            // The account may be written and its generation raised, the entry left behind: its cookie may be dead
            // too, so the page reads the account again.
            _cache.TryRemove(who.Account.Id, out _);
            Said($"  Way in not removed ({name}) from account {who.Account.Id}: {failure.GetType().Name}.");
            return WaysUnavailable();
        }

        if (unlinked.IsFailure)
        {
            return WayRefused(unlinked.Error);
        }

        var account = unlinked.Value;
        _cache[account.Id] = (account, _sessions.Clock.GetUtcNow());
        var cookie = AccountSessions.SetCookie(_sessions.Issue(account), request.Secure);
        if (Devices is { } devices)
        {
            try
            {
                await devices.ForgetAsync(account.Id);
            }
            catch (Exception failure) when (TableRegistry.StoreFails(failure))
            {
                Said($"  Way in removed ({name}) from account {account.Id}, its devices not forgotten: {failure.GetType().Name}.");
                return Problem(503, "Account.Unavailable", "The way to sign in is removed, but your devices could not be signed out. Sign out everywhere from Settings.") with
                {
                    Headers = [new KeyValuePair<string, string>("Set-Cookie", cookie)],
                };
            }
        }

        Said($"  Way in removed ({name}) from account {account.Id}.");
        return Answer(Ways(account), cookie);
    }

    /// <summary>
    /// Null when the posted <c>recent</c> proves this account signed in within <see cref="AccountSessions.RecentFor" />;
    /// otherwise the 403 that asks the player to sign in again. The session cookie alone is not enough to change how
    /// an account is reached: a cookie taken from a device left signed in would hand the account over for good.
    /// </summary>
    private StudioResponse? RecentlySignedIn(Account account, string body)
    {
        var proof = string.IsNullOrWhiteSpace(body) ? null : Proof(body);
        return _sessions.ReadRecent(proof?["recent"]) == account.Id
            ? null
            : Problem(403, "Account.SignInAgain", "For your safety, sign in again to change how you sign in.");
    }

    private static StudioResponse AccountsUnavailable() =>
        Problem(503, "Account.Unavailable", "Accounts cannot be written right now. Try again in a minute.");

    private static StudioResponse WaysUnavailable() =>
        Problem(503, "Account.Unavailable", "Your ways to sign in cannot be changed right now. Try again in a minute.");

    private static StudioResponse WayRefused(DomainError error)
    {
        if (error == AccountErrors.SessionEnded)
        {
            return SignInRequired(error.Message);
        }

        var status = 403;
        if (error == AccountErrors.IdentityNotLinked || error == AccountErrors.NotFound)
        {
            status = 404;
        }
        else if (error == AccountErrors.IdentityTaken || error == AccountErrors.ProviderLinked || error == AccountErrors.LastIdentity)
        {
            status = 409;
        }

        return Problem(status, error.Code, error.Message);
    }

    private static StudioResponse SignInRequired(string message) => Problem(401, SignInRequiredCode, message);

    /// <summary>A sign-in its address's count refuses (#1193), with how long until a place frees.</summary>
    private StudioResponse Limited(string provider, TimeSpan wait)
    {
        Counted(SignInCounts.Step.Limited, provider);
        var seconds = Math.Max(1, (long)Math.Ceiling(wait.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Problem(429, "Account.TooManySignIns", "Too many sign-ins from your network just now. Try again in a minute.") with
        {
            Headers = [new KeyValuePair<string, string>("Retry-After", seconds)],
        };
    }

    /// <summary>Gives the address's place back however the provider answered, and counts the answer: a person vouched for once.</summary>
    private void Settle(AccountRequest request, string provider, SignInAttempts.Outcome outcome, ExternalIdentity? vouched)
    {
        if (request.Address is not { } address)
        {
            return;
        }

        var person = vouched is { } one && one.Provider == provider ? one.ToString() : null;
        _attempts.Settle(address, Clock.GetUtcNow(), person is null ? outcome : SignInAttempts.Outcome.Vouched, person);
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
            Counted(SignInCounts.Step.Refused, identity.Provider);
            return Refused(AccountErrors.RegistrationClosed);
        }

        // A guest who chooses a name becomes that account (ADR 0207): its id, and so its tutorials, are kept.
        if (await GuestAsync(request) is { } guest)
        {
            Result<Account> upgraded;
            try
            {
                upgraded = await _registry.UpgradeAsync(guest.Id, identity, asked!.Username);
            }
            catch (Exception failure) when (TableRegistry.StoreFails(failure))
            {
                // Some of its writes may have gone through: the account is read afresh next time.
                _cache.TryRemove(guest.Id, out _);
                Said($"  Guest not upgraded ({identity.Provider}): account {guest.Id}: {HttpHost.OneLine(failure)}");
                return AccountsUnavailable();
            }

            if (upgraded.IsFailure)
            {
                return Refused(upgraded.Error);
            }

            _cache[guest.Id] = (upgraded.Value, Clock.GetUtcNow());
            Said($"  Guest upgraded ({identity.Provider}): account {guest.Id}.");
            return await SignedInAsync(upgraded.Value, request, identity.Provider);
        }

        var registered = await _registry.RegisterAsync(identity, asked!.Username);
        if (registered.IsFailure && registered.Error == AccountErrors.AlreadyRegistered && await _registry.FindAsync(identity) is { } held)
        {
            // The ticket is the host's own proof of the identity, so the account it reaches signs in: an upgrade asked
            // again after its answer was lost lands here, its guest's cookie ended by the upgrade (ADR 0207).
            return await SignedInAsync(held, request, identity.Provider);
        }

        return registered.IsSuccess ? await SignedInAsync(registered.Value, request, identity.Provider) : Refused(registered.Error);
    }

    /// <summary>
    /// The string fields of a posted object, which is all a provider is given; null for a body that is not one object.
    /// A field that is not text is left out, so the provider reads it as missing. Names are read whatever their case,
    /// as the route's JSON was before providers (<c>Credential</c> signed in then too).
    /// </summary>
    private static SignInProof? Proof(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return SignInProof.Of();
        }

        using var posted = JsonDocument.Parse(body);
        if (posted.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in posted.RootElement.EnumerateObject())
        {
            if (field.Value.ValueKind == JsonValueKind.String)
            {
                fields[field.Name] = field.Value.GetString()!;
            }
        }

        return new SignInProof(fields);
    }

    /// <summary>One step of a sign-in, counted by provider and said with the minute's others; how many the minute holds.</summary>
    private long Counted(SignInCounts.Step step, string provider) => _counts.Add(provider, step, Clock.GetUtcNow());

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
        Counted(SignInCounts.Step.Started, identity.Provider);
        if (await _registry.FindAsync(identity) is { } account)
        {
            return await SignedInAsync(account, request, identity.Provider);
        }

        var registered = await _registry.RegisterAsync(identity, name.Value.Value);
        return registered.IsSuccess ? await SignedInAsync(registered.Value, request, identity.Provider) : Refused(registered.Error);
    }

    private async Task<StudioResponse> SignOutAsync(string body, AccountRequest request)
    {
        var asked = Read<SignOutBody>(body);
        if (asked?.Everywhere == true && await WhoAsync(request) is { } who)
        {
            await SignOutEverywhereAsync(who.Account.Id);
        }
        else if (!string.IsNullOrWhiteSpace(asked?.Endpoint) && Devices is { } devices && await WhoAsync(request) is { } leaving)
        {
            // A browser signing out is no longer the account's device (ADR 0202): a borrowed one, whose seat switch
            // made it one, is told nothing more of the account once its player has left it.
            try
            {
                await devices.RemoveAsync(leaving.Account.Id, asked.Endpoint);
            }
            catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is JsonException or InvalidDataException)
            {
                Console.WriteLine($"  Account {leaving.Account.Id}: a device signing out could not be forgotten: {HttpHost.OneLine(failure)}");
            }
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
            return Problem(401, SignInRequiredCode, "Sign in to delete your account.");
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
            return Problem(401, SignInRequiredCode, "Sign in to be notified on this device.");
        }

        var sent = Read<BrowserSubscription>(body);
        var subscription = PushSubscription.From(sent?.Endpoint, sent?.Keys?.P256dh, sent?.Keys?.Auth);
        if (subscription is null)
        {
            return Problem(400, "Account.BadSubscription", "Send the subscription as the browser serialises it: an https endpoint, a p256dh key of 65 bytes and an auth secret of 16.");
        }

        if (!PushSubscription.OnKnownService(subscription.Endpoint))
        {
            return Problem(400, "Account.UnknownPushService", PushTest.UnknownServiceMessage);
        }

        if (await notices.IsGoneAsync(who.Account.Id, subscription.Endpoint))
        {
            return Problem(410, "Account.DeviceGone", "This subscription is gone from its push service. Subscribe again.");
        }

        var held = await devices.AddAsync(who.Account.Id, new PushDevice(subscription.Endpoint, subscription.P256dh, subscription.Auth, default));

        // Asked again after the write: a "sign out everywhere" or a suspension that forgot the devices while this one
        // was being written (its write then reads the list again and makes it anew) must not leave a lost or lent
        // device told for good (ADR 0201). The revocation marks the account before it forgets the devices.
        if (await WhoAsync(request) is null)
        {
            await devices.RemoveAsync(who.Account.Id, subscription.Endpoint);
            return Problem(401, SignInRequiredCode, "Sign in to be notified on this device.");
        }

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
            return SignInRequired(SignInFirst);
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
            return SignInRequired(SignInFirst);
        }

        var test = await notices.TestAsync(who.Account.Id, Read<BrowserSubscription>(body)?.Endpoint);
        if (test.UnknownService)
        {
            return Problem(400, "Account.UnknownPushService", test.Message);
        }

        return StudioResponse.OfJson(new { sent = test.Sent, message = test.Message }, Web, test.Sent ? 200 : 502);
    }

    /// <summary>
    /// The account's notice log (ADR 0201), newest first, and how many entries are unread: what the bell's news reads.
    /// Signed in; 404 on a host that keeps none.
    /// </summary>
    private async Task<StudioResponse> NoticesAsync(AccountRequest request)
    {
        if (NoticeLog is not { } log)
        {
            return Problem(404, "Account.NoNoticeLog", "This host keeps no notices.");
        }

        if (await WhoAsync(request) is not { } who)
        {
            return SignInRequired(SignInFirst);
        }

        try
        {
            var entries = await log.ListAsync(who.Account.Id);
            return Answer(new { notices = entries.Select(DescribeNotice), unread = entries.Count(entry => !entry.Read) }, who.Renewed);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is JsonException or InvalidDataException)
        {
            Console.WriteLine($"  Account {who.Account.Id}: its notices could not be read: {HttpHost.OneLine(failure)}");
            return Problem(503, "Account.NoticesUnavailable", "The notices could not be read. Try again in a moment.");
        }
    }

    /// <summary>Marks read every entry of the log up to the id the page saw last, that one included. How many it marked.</summary>
    private async Task<StudioResponse> ReadNoticesAsync(string body, AccountRequest request)
    {
        if (NoticeLog is not { } log)
        {
            return Problem(404, "Account.NoNoticeLog", "This host keeps no notices.");
        }

        if (await WhoAsync(request) is not { } who)
        {
            return SignInRequired(SignInFirst);
        }

        if (Read<ReadNoticesBody>(body)?.UpTo is not { } upTo)
        {
            return Problem(400, "Account.BadNoticeRead", "Say up to which notice, by its id: { \"upTo\": 123 }.");
        }

        try
        {
            var marked = await log.MarkReadAsync(who.Account.Id, upTo);
            if (marked > 0)
            {
                // Only the bell shows what was read: the account's lists stay as they are (#1152).
                Changes?.Notices(who.Account.Id);
            }

            return Answer(new { marked }, who.Renewed);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is JsonException or InvalidDataException)
        {
            Console.WriteLine($"  Account {who.Account.Id}: its notices could not be marked read: {HttpHost.OneLine(failure)}");
            return Problem(503, "Account.NoticesUnavailable", "The notices could not be marked read. Try again in a moment.");
        }
    }

    private static object DescribeNotice(NoticeEntry entry) => new
    {
        id = entry.Id,
        kind = NoticeKinds.Name(entry.Kind),
        title = entry.Title,
        body = entry.Body,
        url = entry.Url,
        at = entry.At,
        read = entry.Read,
    };

    /// <summary>The settings every new game of the account starts from (ADR 0140): the saved ones, or the defaults.</summary>
    private async Task<StudioResponse> PreferencesAsync(AccountRequest request)
    {
        if (Preferences is not { } store)
        {
            return Problem(404, "Account.NoPreferences", "This host keeps no game settings.");
        }

        if (await WhoAsync(request) is not { } who)
        {
            return SignInRequired(SignInFirst);
        }

        var saved = await store.GetAsync(who.Account.Id);
        var tutorials = await store.GetTutorialsAsync(who.Account.Id);
        var notices = await NoticesOrDefaultAsync(store, who.Account.Id);
        return Answer(new { preferences = DescribePreferences(saved ?? GamePreferences.Default), saved = saved is not null, tutorials = new { done = tutorials.Done, skipped = tutorials.Skipped }, notices = DescribeNotices(notices) }, who.Renewed);
    }

    /// <summary>
    /// Replaces what pops up on the account's devices (ADR 0202): its turns, its invitations and friends, its results.
    /// A choice left out pops up. The bell keeps every notice whatever is chosen here.
    /// </summary>
    private async Task<StudioResponse> SaveNoticePreferencesAsync(string body, AccountRequest request)
    {
        if (Preferences is not { } store)
        {
            return Problem(404, "Account.NoPreferences", "This host keeps no game settings.");
        }

        if (await WhoAsync(request) is not { } who)
        {
            return SignInRequired(SignInFirst);
        }

        var asked = Read<NoticePreferencesBody>(body);
        var chosen = new NoticePreferences(asked?.Turns ?? true, asked?.Invitations ?? true, asked?.Results ?? true);
        try
        {
            await store.SaveNoticesAsync(who.Account.Id, chosen);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure))
        {
            Console.WriteLine($"  Account {who.Account.Id}: what pops up could not be saved: {HttpHost.OneLine(failure)}");
            return Problem(503, "Account.PreferencesUnavailable", "Your choices could not be saved just now. Try again.");
        }

        return Answer(new { notices = DescribeNotices(chosen) }, who.Renewed);
    }

    // What pops up is a line of the settings' answer, never a reason to lose the rest of it: a store that fails reads
    // as every kind, which is what the door does too when it cannot read them.
    private static async Task<NoticePreferences> NoticesOrDefaultAsync(IPreferenceStore store, AccountId account)
    {
        try
        {
            return await store.GetNoticesAsync(account);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is JsonException)
        {
            Console.WriteLine($"  Account {account}: what pops up could not be read: {HttpHost.OneLine(failure)}");
            return NoticePreferences.Default;
        }
    }

    private static object DescribeNotices(NoticePreferences notices) => new { turns = notices.Turns, invitations = notices.Invitations, results = notices.Results };

    /// <summary>Replaces the account's settings; a game already open keeps what it was set to.</summary>
    private async Task<StudioResponse> SavePreferencesAsync(string body, AccountRequest request)
    {
        if (Preferences is not { } store)
        {
            return Problem(404, "Account.NoPreferences", "This host keeps no game settings.");
        }

        if (await WhoAsync(request) is not { } who)
        {
            return SignInRequired(SignInFirst);
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

    private async Task<StudioResponse> SignedInAsync(Account account, AccountRequest request, string provider)
    {
        // A suspended account is told why, and given no session (ADR 0178).
        if (account.Suspension is { } suspension)
        {
            Counted(SignInCounts.Step.Refused, provider);
            var refused = AccountErrors.Suspended(suspension);
            return Problem(403, refused.Code, refused.Message);
        }

        if (Activity is { } activity)
        {
            await activity.SignedInAsync(account.Id);
        }

        var cookie = AccountSessions.SetCookie(_sessions.Issue(account), request.Secure);
        Counted(SignInCounts.Step.Finished, provider);
        return Answer(new { account = Describe(account), recent = _sessions.Recent(account) }, cookie);
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

    private sealed record NoticePreferencesBody(bool? Turns, bool? Invitations, bool? Results);

    private sealed record PreferencesBody(string? Pace, decimal? Speed, bool? Help, bool? ReducedMotion, bool? Reactions);

    private sealed record RegisterBody(string? Ticket, string? Username);

    private sealed record SignOutBody(bool? Everywhere, string? Endpoint);

    private sealed record ReadNoticesBody(long? UpTo);

    private sealed record BrowserSubscription(string? Endpoint, BrowserKeys? Keys);

    private sealed record BrowserKeys(string? P256dh, string? Auth);
}
