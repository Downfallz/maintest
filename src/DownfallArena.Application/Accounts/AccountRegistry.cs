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
        if (!await ReserveAsync(reservation, cancellationToken))
        {
            return Result.Failure<Account>(AccountErrors.UsernameTaken);
        }

        var account = new Account(reservation.Account, identity, name.Value, now, Generation: 1);
        await _store.SaveAsync(account, cancellationToken);
        if (!await _store.TryBindSubjectAsync(identity, account.Id, cancellationToken))
        {
            // The same sign-in registered at the same moment, and the other registration bound it first.
            await _store.DeleteAsync(account.Id, cancellationToken);
            await _store.ReleaseUsernameAsync(reservation, cancellationToken);
            return Result.Failure<Account>(AccountErrors.AlreadyRegistered);
        }

        return Result.Success(account);
    }

    private async Task<bool> ReserveAsync(UsernameReservation reservation, CancellationToken cancellationToken)
    {
        if (await _store.TryReserveUsernameAsync(reservation, cancellationToken))
        {
            return true;
        }

        var holder = await _store.FindUsernameAsync(reservation.Username, cancellationToken);
        if (holder is not null && !await AbandonedAsync(holder, cancellationToken))
        {
            return false;
        }

        if (holder is not null)
        {
            await _store.ReleaseUsernameAsync(holder, cancellationToken);
        }

        return await _store.TryReserveUsernameAsync(reservation, cancellationToken);
    }

    private async Task<bool> AbandonedAsync(UsernameReservation holder, CancellationToken cancellationToken) =>
        _clock.GetUtcNow() - holder.ReservedAt >= AbandonedAfter && await _store.FindAsync(holder.Account, cancellationToken) is null;
}
