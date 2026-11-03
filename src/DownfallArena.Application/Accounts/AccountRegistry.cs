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

    // A generation is read, raised and written back: two at once would both write the same number, and one that
    // stalled could write back a number already passed, bringing revoked sessions back. One host runs at most one
    // replica (ADR 0080), so one lock in the process is the whole of the ordering.
    private static readonly SemaphoreSlim Generations = new(1, 1);

    public AccountRegistry(IAccountStore store, TimeProvider clock, Func<AccountId>? newId = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _clock = clock;
        _newId = newId ?? AccountId.New;
    }

    /// <summary>The account this sign-in registered, or null when it has none yet.</summary>
    public async Task<Account?> FindAsync(ExternalIdentity identity, CancellationToken cancellationToken = default)
    {
        var id = await _store.FindBySubjectAsync(identity, cancellationToken);
        return id is { } bound ? await _store.FindAsync(bound, cancellationToken) : null;
    }

    public Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default) => _store.FindAsync(id, cancellationToken);

    /// <summary>
    /// Raises the account's generation, so every session issued before is refused: "sign out everywhere". Null
    /// when there is no such account.
    /// </summary>
    public async Task<Account?> SignOutEverywhereAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        await Generations.WaitAsync(cancellationToken);
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
            Generations.Release();
        }
    }

    /// <summary>
    /// Removes the account (ADR 0122): its sign-in first, so nobody signs in to it again, then the account, then its
    /// name, which another player may then take. The same sign-in can register afresh afterwards, under a new id.
    /// False when there is no such account.
    /// </summary>
    public async Task<bool> DeleteAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        if (await _store.FindAsync(id, cancellationToken) is not { } account)
        {
            return false;
        }

        await _store.UnbindSubjectAsync(account.Identity, id, cancellationToken);
        await _store.DeleteAsync(id, cancellationToken);
        await _store.ReleaseUsernameAsync(new UsernameReservation(account.Username, id, account.CreatedAt), cancellationToken);
        return true;
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
        if (name.IsFailure)
        {
            return Result.Failure<Account>(name.Error);
        }

        if (await _store.FindBySubjectAsync(identity, cancellationToken) is { } bound)
        {
            if (await _store.FindAsync(bound, cancellationToken) is not null)
            {
                return Result.Failure<Account>(AccountErrors.AlreadyRegistered);
            }

            // A binding to an account that no longer exists holds nobody: the account was deleted and the
            // deletion stopped before its binding went.
            await _store.UnbindSubjectAsync(identity, bound, cancellationToken);
        }

        var now = _clock.GetUtcNow();
        var reservation = new UsernameReservation(name.Value, _newId(), now);
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
            : new Account(reservation.Account, identity, name.Value, now, Generation: 1);
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
            if (account is not null && account.Identity == identity)
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
    /// A name is abandoned when it has been held an hour for an account that was never written, or for one whose
    /// sign-in was never bound to it: both are a registration that stopped between its writes.
    /// </summary>
    private async Task<bool> AbandonedAsync(UsernameReservation holder, Account? account, CancellationToken cancellationToken) =>
        _clock.GetUtcNow() - holder.ReservedAt >= AbandonedAfter
        && (account is null || await _store.FindBySubjectAsync(account.Identity, cancellationToken) != account.Id);
}
