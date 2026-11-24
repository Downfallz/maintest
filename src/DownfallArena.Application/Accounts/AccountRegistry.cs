using System.Security.Cryptography;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// Signing in and registering (ADR 0122). A sign-in the store knows is its account; one it does not needs a
/// name first. Registration writes the name, then the account, then the sign-in, each write refused if somebody
/// got there first, so two people asking for one name, or one person registering twice at once, end with one
/// account and no lock.
/// </summary>
public sealed class AccountRegistry
{
    /// <summary>
    /// How long a name held for an account that was never written stays held. A registration that stopped between
    /// its two writes leaves such a name; another registration of a just-held name is far more likely to be a race
    /// still in flight, so it is not taken back at once.
    /// </summary>
    public static readonly TimeSpan AbandonedAfter = TimeSpan.FromHours(1);

    private readonly IAccountStore _store;
    private readonly TimeProvider _clock;
    private readonly Func<AccountId> _newId;
    private readonly Func<string> _guestCode;

    /// <summary>How many names a new guest is drawn before the host gives up on making it: each is one in nearly a million.</summary>
    public const int GuestDraws = 8;

    // A generation is read, raised and written back: two at once would both write the same number, and one that
    // stalled could write back a number already passed, bringing revoked sessions back -- or write back an account
    // deleted meanwhile, bringing it back without its indexes. Raising and deleting take this one lock; one host
    // runs at most one replica (ADR 0080), so one lock in the process is the whole of the ordering.
    private static readonly SemaphoreSlim Writes = new(1, 1);

    /// <param name="guestCode">Draws a guest's code (ADR 0207); a random one of <see cref="Username.GuestAlphabet" /> unless a test draws its own.</param>
    public AccountRegistry(IAccountStore store, TimeProvider clock, Func<AccountId>? newId = null, Func<string>? guestCode = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _clock = clock;
        _newId = newId ?? AccountId.New;
        _guestCode = guestCode ?? DrawGuestCode;
    }

    private static string DrawGuestCode() => RandomNumberGenerator.GetString(Username.GuestAlphabet, Username.GuestCodeLength);

    /// <summary>
    /// Makes a guest (ADR 0207): an account no provider vouches for, under an identity of the host's own, a random
    /// <c>guest:&lt;subject&gt;</c> nobody can sign in with, and a name the host draws (<c>Guest-7KQ2</c>). It is written
    /// as a registration is, the name held first. A name somebody holds is drawn again, <see cref="GuestDraws" /> times
    /// at most, then refused as taken.
    /// </summary>
    public async Task<Result<Account>> MakeGuestAsync(CancellationToken cancellationToken = default)
    {
        var identity = new ExternalIdentity(ExternalIdentity.Guest, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)));
        for (var draw = 0; draw < GuestDraws; draw++)
        {
            var made = await RegisterAsync(identity, Username.Guest(_guestCode()), cancellationToken);
            if (made.IsSuccess || made.Error != AccountErrors.UsernameTaken)
            {
                return made;
            }
        }

        return Result.Failure<Account>(AccountErrors.UsernameTaken);
    }

    /// <summary>
    /// Upgrades a guest (ADR 0207) with the identity a provider just vouched for and the name its player chose: the
    /// account keeps its id, so its tutorials, and loses the guest's identity and name. The writes go in the order a
    /// stop between any two leaves the account reachable: the new name held, the identity bound to the account, the
    /// account written with that identity alone and a raised generation (every other copy of the guest's cookie ends),
    /// then the guest's binding and name freed. Asking again finishes an upgrade that stopped before the account was
    /// written; one that stopped after leaves the guest's binding, which reaches nothing, and its name held. Refused for an account
    /// that is not a guest, an identity another account holds, and a name somebody else holds.
    /// </summary>
    public async Task<Result<Account>> UpgradeAsync(AccountId id, ExternalIdentity identity, string? candidate, CancellationToken cancellationToken = default)
    {
        if (identity.Provider == ExternalIdentity.Guest)
        {
            throw new ArgumentException("A guest is upgraded with a provider's identity, not another guest's.", nameof(identity));
        }

        var name = Username.Create(candidate);
        if (name.IsFailure)
        {
            return Result.Failure<Account>(name.Error);
        }

        await Writes.WaitAsync(cancellationToken);
        try
        {
            if (await _store.FindAsync(id, cancellationToken) is not { } account)
            {
                return Result.Failure<Account>(AccountErrors.NotFound);
            }

            if (account.Identities.ByProvider(ExternalIdentity.Guest) is not { } guest)
            {
                return Result.Failure<Account>(AccountErrors.NotAGuest);
            }

            if (account.Suspension is { } suspension)
            {
                return Result.Failure<Account>(AccountErrors.Suspended(suspension));
            }

            var reservation = new UsernameReservation(name.Value, id, _clock.GetUtcNow());
            if (await ReserveAsync(reservation, guest, cancellationToken) is not { } held || held.Account != id)
            {
                return Result.Failure<Account>(AccountErrors.UsernameTaken);
            }

            if (!await BindAsync(identity, id, cancellationToken))
            {
                await ReleaseUnlessNamedAsync(reservation, account, cancellationToken);
                return Result.Failure<Account>(AccountErrors.AlreadyRegistered);
            }

            var upgraded = account with { Identities = AccountIdentities.Of(identity), Username = name.Value, Generation = account.Generation + 1 };
            await _store.SaveAsync(upgraded, cancellationToken);
            await _store.UnbindSubjectAsync(guest, id, cancellationToken);
            if (account.Username.Key != name.Value.Key)
            {
                await _store.ReleaseUsernameAsync(new UsernameReservation(account.Username, id, account.CreatedAt), cancellationToken);
            }

            return Result.Success(upgraded);
        }
        finally
        {
            Writes.Release();
        }
    }

    // A name held for an upgrade refused afterwards is freed again, unless it is the one the account already has.
    private async Task ReleaseUnlessNamedAsync(UsernameReservation reservation, Account account, CancellationToken cancellationToken)
    {
        if (reservation.Username.Key != account.Username.Key)
        {
            await _store.ReleaseUsernameAsync(reservation, cancellationToken);
        }
    }

    /// <summary>
    /// The account this sign-in reaches, or null when it has none. A binding to an account that does not list the
    /// identity (a link or a removal that stopped between its writes, ADR 0204) reaches nothing: the account's list,
    /// which the player sees, is the whole of its ways in.
    /// </summary>
    public async Task<Account?> FindAsync(ExternalIdentity identity, CancellationToken cancellationToken = default)
    {
        var id = await _store.FindBySubjectAsync(identity, cancellationToken);
        return id is { } bound && await _store.FindAsync(bound, cancellationToken) is { } account && account.Identities.Contains(identity) ? account : null;
    }

    public Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default) => _store.FindAsync(id, cancellationToken);

    /// <summary>
    /// Raises the account's generation, so every session issued before is refused: "sign out everywhere". Null
    /// when there is no such account.
    /// </summary>
    public async Task<Account?> SignOutEverywhereAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            if (await _store.FindAsync(id, cancellationToken) is not { } account)
            {
                return null;
            }

            var raised = account with { Generation = account.Generation + 1 };
            await _store.SaveAsync(raised, cancellationToken);
            return raised;
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>
    /// Suspends the account (ADR 0178): the suspension written and the generation raised in one write, so every session
    /// of it stops working at once and no new one is issued until it is restored.
    /// </summary>
    public Task<Result<Account>> SuspendAsync(AccountId id, AccountSuspension suspension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(suspension);
        return ChangeAsync(
            id,
            account => account.Suspension is null
                ? Result.Success(account with { Generation = account.Generation + 1, Suspension = suspension })
                : Result.Failure<Account>(AccountErrors.AlreadySuspended),
            cancellationToken);
    }

    /// <summary>Lifts the account's suspension (ADR 0178): it signs in again, and whatever seats it still holds are its own.</summary>
    public Task<Result<Account>> RestoreAsync(AccountId id, CancellationToken cancellationToken = default) =>
        ChangeAsync(
            id,
            account => account.Suspension is null
                ? Result.Failure<Account>(AccountErrors.NotSuspended)
                : Result.Success(account with { Suspension = null }),
            cancellationToken);

    // One account read, changed and written back under the lock every write of an account takes.
    private async Task<Result<Account>> ChangeAsync(AccountId id, Func<Account, Result<Account>> change, CancellationToken cancellationToken)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            if (await _store.FindAsync(id, cancellationToken) is not { } account)
            {
                return Result.Failure<Account>(AccountErrors.NotFound);
            }

            var changed = change(account);
            if (changed.IsSuccess)
            {
                await _store.SaveAsync(changed.Value, cancellationToken);
            }

            return changed;
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>
    /// Adds a way into the account (ADR 0204), for a player signed in to it. The identity must come from a provider's
    /// sign-in completed in the same request, never from what the request says it is. The identity's binding is
    /// written first, create-only-if-absent, so an identity another account holds is refused however close the race,
    /// then the account. A binding already naming this account is a link that stopped between its writes, finished
    /// now; one naming an account that is gone or does not list it holds nobody and is taken back. A suspended
    /// account links nothing, nor a second identity of a provider it already holds. Sessions are left alone.
    /// <paramref name="generation" />, when given, is the asking session's: a sign out everywhere that raised it while
    /// the provider was being asked refuses the link, checked under the same gate as the write.
    /// </summary>
    public async Task<Result<Account>> LinkAsync(AccountId id, ExternalIdentity identity, int? generation = null, CancellationToken cancellationToken = default)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            if (await _store.FindAsync(id, cancellationToken) is not { } account)
            {
                return Result.Failure<Account>(AccountErrors.NotFound);
            }

            if (account.Suspension is { } suspension)
            {
                return Result.Failure<Account>(AccountErrors.Suspended(suspension));
            }

            // A guest gains a way in by its upgrade alone (ADR 0207), which also gives it a name of its own.
            if (account.IsGuest || identity.Provider == ExternalIdentity.Guest)
            {
                return Result.Failure<Account>(AccountErrors.GuestNotAllowed);
            }

            if (generation is { } asked && asked != account.Generation)
            {
                return Result.Failure<Account>(AccountErrors.SessionEnded);
            }

            if (account.Identities.Contains(identity))
            {
                return Result.Success(account);
            }

            if (account.Identities.ByProvider(identity.Provider) is not null)
            {
                return Result.Failure<Account>(AccountErrors.ProviderLinked);
            }

            if (!await BindAsync(identity, id, cancellationToken))
            {
                return Result.Failure<Account>(AccountErrors.IdentityTaken);
            }

            var linked = account with { Identities = account.Identities.With(identity) };
            await _store.SaveAsync(linked, cancellationToken);
            return Result.Success(linked);
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>
    /// Removes a way into the account (ADR 0204); the last one stays, since deleting the account is how that goes.
    /// The removal raises the generation in the same write, so whoever signed in through the identity removed is
    /// signed out with every other session. The account is written first, then the binding removed. If the removal
    /// stops between the two, the binding reaches nothing, since the account no longer lists it, and no other
    /// account can take the identity while this one lists it. Removing it again finishes the job.
    /// <paramref name="generation" />, when given, is the asking session's: one ended meanwhile removes nothing.
    /// </summary>
    public async Task<Result<Account>> UnlinkAsync(AccountId id, ExternalIdentity identity, int? generation = null, CancellationToken cancellationToken = default)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            if (await _store.FindAsync(id, cancellationToken) is not { } account)
            {
                return Result.Failure<Account>(AccountErrors.NotFound);
            }

            if (generation is { } asked && asked != account.Generation)
            {
                return Result.Failure<Account>(AccountErrors.SessionEnded);
            }

            var remaining = account.Identities.Without(identity);
            if (remaining.IsFailure)
            {
                return remaining.Error == AccountErrors.IdentityNotLinked && await _store.UnbindSubjectAsync(identity, id, cancellationToken)
                    ? Result.Success(account)
                    : Result.Failure<Account>(remaining.Error);
            }

            var unlinked = account with { Identities = remaining.Value, Generation = account.Generation + 1 };
            await _store.SaveAsync(unlinked, cancellationToken);
            await _store.UnbindSubjectAsync(identity, id, cancellationToken);
            return Result.Success(unlinked);
        }
        finally
        {
            Writes.Release();
        }
    }

    // Binds the identity to the account; true when it now names it, already or once a binding that holds nobody is
    // taken back, false when another account holds it.
    private async Task<bool> BindAsync(ExternalIdentity identity, AccountId id, CancellationToken cancellationToken)
    {
        if (await _store.TryBindSubjectAsync(identity, id, cancellationToken))
        {
            return true;
        }

        var holder = await _store.FindBySubjectAsync(identity, cancellationToken);
        if (holder == id)
        {
            return true;
        }

        if (holder is { } other)
        {
            if (!await HoldsNobodyAsync(identity, other, cancellationToken))
            {
                return false;
            }

            await _store.UnbindSubjectAsync(identity, other, cancellationToken);
        }

        return await _store.TryBindSubjectAsync(identity, id, cancellationToken)
            || await _store.FindBySubjectAsync(identity, cancellationToken) == id;
    }

    /// <summary>
    /// Removes the account (ADR 0122): its sign-ins first, so nobody signs in to it again, then the account, then its
    /// name, which another player may then take. The same sign-ins can register afresh afterwards, under a new id.
    /// False when there is no such account.
    /// </summary>
    public async Task<bool> DeleteAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            if (await _store.FindAsync(id, cancellationToken) is not { } account)
            {
                return false;
            }

            foreach (var identity in account.Identities.All)
            {
                await _store.UnbindSubjectAsync(identity, id, cancellationToken);
            }

            await _store.DeleteAsync(id, cancellationToken);
            await _store.ReleaseUsernameAsync(new UsernameReservation(account.Username, id, account.CreatedAt), cancellationToken);
            return true;
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>
    /// Gives the account another name (ADR 0174), checked as a name chosen at registration is: the new name is held
    /// first, then the account written, then the old name freed for another player. A change of case alone keeps the
    /// name it holds. Sessions are left alone: they name the account, not its name.
    /// </summary>
    public async Task<Result<Account>> RenameAsync(AccountId id, string? candidate, CancellationToken cancellationToken = default)
    {
        var name = Username.Create(candidate);
        if (name.IsFailure)
        {
            return Result.Failure<Account>(name.Error);
        }

        await Writes.WaitAsync(cancellationToken);
        try
        {
            if (await _store.FindAsync(id, cancellationToken) is not { } account)
            {
                return Result.Failure<Account>(AccountErrors.NotFound);
            }

            var renamed = account with { Username = name.Value };
            if (name.Value.Key == account.Username.Key)
            {
                await _store.SaveAsync(renamed, cancellationToken);
                return Result.Success(renamed);
            }

            var reservation = new UsernameReservation(name.Value, id, _clock.GetUtcNow());
            if (await ReserveAsync(reservation, account.Identities.All[0], cancellationToken) is not { } held || held.Account != id)
            {
                return Result.Failure<Account>(AccountErrors.UsernameTaken);
            }

            await _store.SaveAsync(renamed, cancellationToken);
            if (await _store.FindUsernameAsync(account.Username, cancellationToken) is { } old && old.Account == id)
            {
                await _store.ReleaseUsernameAsync(old, cancellationToken);
            }

            return Result.Success(renamed);
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>Whether a name could be registered now: what the page says while a player types.</summary>
    public async Task<Result> CheckUsernameAsync(string? candidate, CancellationToken cancellationToken = default)
    {
        var name = Username.Create(candidate);
        if (name.IsFailure)
        {
            return Result.Failure(name.Error);
        }

        var holder = await _store.FindUsernameAsync(name.Value, cancellationToken);
        return holder is null || await AbandonedAsync(holder, cancellationToken) ? Result.Success() : Result.Failure(AccountErrors.UsernameTaken);
    }

    public async Task<Result<Account>> RegisterAsync(ExternalIdentity identity, string? candidate, CancellationToken cancellationToken = default)
    {
        var name = Username.Create(candidate);
        return name.IsFailure ? Result.Failure<Account>(name.Error) : await RegisterAsync(identity, name.Value, cancellationToken);
    }

    private async Task<Result<Account>> RegisterAsync(ExternalIdentity identity, Username name, CancellationToken cancellationToken)
    {
        if (await _store.FindBySubjectAsync(identity, cancellationToken) is { } bound && !await ReleaseIfHoldsNobodyAsync(identity, bound, cancellationToken))
        {
            return Result.Failure<Account>(AccountErrors.AlreadyRegistered);
        }

        var now = _clock.GetUtcNow();
        var reservation = new UsernameReservation(name, _newId(), now);
        var held = await ReserveAsync(reservation, identity, cancellationToken);
        if (held is null)
        {
            return Result.Failure<Account>(AccountErrors.UsernameTaken);
        }

        // An account this sign-in wrote before the registration stopped short of binding it is taken up again,
        // rather than left holding the name the player is asking for once more.
        var resumed = held.Account != reservation.Account;
        var account = resumed
            ? await _store.FindAsync(held.Account, cancellationToken)
            : new Account(reservation.Account, AccountIdentities.Of(identity), name, now, Generation: 1);
        if (account is null)
        {
            // The interrupted account went between reading its name and reading it: somebody reclaimed both.
            return Result.Failure<Account>(AccountErrors.UsernameTaken);
        }

        if (!resumed)
        {
            await _store.SaveAsync(account, cancellationToken);
        }

        if (!await _store.TryBindSubjectAsync(identity, account.Id, cancellationToken)
            && await _store.FindBySubjectAsync(identity, cancellationToken) != account.Id)
        {
            // The same sign-in registered at the same moment, and the other registration bound it first.
            if (!resumed)
            {
                await _store.DeleteAsync(account.Id, cancellationToken);
                await _store.ReleaseUsernameAsync(reservation, cancellationToken);
            }

            return Result.Failure<Account>(AccountErrors.AlreadyRegistered);
        }

        return Result.Success(account);
    }

    /// <summary>
    /// Removes a binding that holds nobody, and says whether it did: one to an account that is gone (a deletion
    /// stopped before its bindings went) or that does not list the identity (a link or a removal that stopped between
    /// its writes, ADR 0204). Under the lock links and removals take, so a link in progress is never taken for one
    /// that stopped.
    /// </summary>
    private async Task<bool> ReleaseIfHoldsNobodyAsync(ExternalIdentity identity, AccountId bound, CancellationToken cancellationToken)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            if (!await HoldsNobodyAsync(identity, bound, cancellationToken))
            {
                return false;
            }

            await _store.UnbindSubjectAsync(identity, bound, cancellationToken);
            return true;
        }
        finally
        {
            Writes.Release();
        }
    }

    private async Task<bool> HoldsNobodyAsync(ExternalIdentity identity, AccountId bound, CancellationToken cancellationToken) =>
        await _store.FindAsync(bound, cancellationToken) is not { } account || !account.Identities.Contains(identity);

    /// <summary>
    /// The reservation that holds the name for this registration: its own, or one an interrupted registration of
    /// the same sign-in left; null when somebody else holds the name.
    /// </summary>
    private async Task<UsernameReservation?> ReserveAsync(UsernameReservation reservation, ExternalIdentity identity, CancellationToken cancellationToken)
    {
        if (await _store.TryReserveUsernameAsync(reservation, cancellationToken))
        {
            return reservation;
        }

        var holder = await _store.FindUsernameAsync(reservation.Username, cancellationToken);
        if (holder is not null)
        {
            var account = await _store.FindAsync(holder.Account, cancellationToken);
            if (account is not null && account.Identities.Contains(identity))
            {
                return holder;
            }

            if (!await AbandonedAsync(holder, account, cancellationToken))
            {
                return null;
            }

            await _store.ReleaseUsernameAsync(holder, cancellationToken);
            if (account is not null)
            {
                await _store.DeleteAsync(account.Id, cancellationToken);
            }
        }

        return await _store.TryReserveUsernameAsync(reservation, cancellationToken) ? reservation : null;
    }

    private async Task<bool> AbandonedAsync(UsernameReservation holder, CancellationToken cancellationToken) =>
        await AbandonedAsync(holder, await _store.FindAsync(holder.Account, cancellationToken), cancellationToken);

    /// <summary>
    /// A name is abandoned when it has been held an hour for an account that was never written, or for one none of
    /// whose identities is bound to it: both are a registration that stopped between its writes.
    /// </summary>
    private async Task<bool> AbandonedAsync(UsernameReservation holder, Account? account, CancellationToken cancellationToken) =>
        _clock.GetUtcNow() - holder.ReservedAt >= AbandonedAfter
        && (account is null || !await AnyBoundAsync(account, cancellationToken));

    private async Task<bool> AnyBoundAsync(Account account, CancellationToken cancellationToken)
    {
        foreach (var identity in account.Identities.All)
        {
            if (await _store.FindBySubjectAsync(identity, cancellationToken) == account.Id)
            {
                return true;
            }
        }

        return false;
    }
}
