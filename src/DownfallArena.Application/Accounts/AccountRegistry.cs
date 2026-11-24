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
    // runs at most one replica (ADR 0080), so one lock orders the writes of one process. During a deploy the release
    // beside this one writes the same accounts, which no lock here reaches: every write of an account read before is
    // conditional on the version read, and read again when the other got there first (#1217).
    private static readonly SemaphoreSlim Writes = new(1, 1);

    /// <summary>How many times a write of an account is read and tried again while another process writes it first.</summary>
    private const int Tries = 5;

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
    /// then the guest's binding and name freed. The account keeps the guest's identity as <see cref="Account.Unbinding" />
    /// until its binding is gone. Asking again finishes an upgrade that stopped before the account was written. Asked with
    /// another name, the first name is freed an hour later (<see cref="AbandonedAfter" />, #1227). An upgrade that stopped
    /// after the account was written leaves the guest's binding to the next removal, link or deletion, and the drawn name
    /// held for an hour. Refused for an account
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
            UsernameReservation? reservation = null;
            Account? before = null;
            Account? last = null;
            var bound = false;
            var upgraded = await WriteAsync(
                id,
                async account =>
                {
                    last = account;
                    if (account.Identities.ByProvider(ExternalIdentity.Guest) is not { } guest)
                    {
                        return Result.Failure<Account>(AccountErrors.NotAGuest);
                    }

                    if (account.Suspension is { } suspension)
                    {
                        return Result.Failure<Account>(AccountErrors.Suspended(suspension));
                    }

                    before = account;
                    reservation ??= new UsernameReservation(name.Value, id, _clock.GetUtcNow());
                    if (await ReserveAsync(reservation, guest, cancellationToken) is not { } held || held.Account != id)
                    {
                        return Result.Failure<Account>(AccountErrors.UsernameTaken);
                    }

                    var binding = await BindAsync(identity, id, cancellationToken);
                    bound |= binding.Made;
                    if (!binding.Bound)
                    {
                        await ReleaseUnlessNamedAsync(reservation, account, cancellationToken);
                        return Result.Failure<Account>(AccountErrors.AlreadyRegistered);
                    }

                    return Result.Success(account with
                    {
                        Identities = AccountIdentities.Of(identity),
                        Username = name.Value,
                        Generation = account.Generation + 1,
                        Unbinding = guest,
                    });
                },
                cancellationToken);
            if (upgraded.IsFailure)
            {
                // A try refused after an earlier one held the name and bound the identity: another process changed the
                // account meanwhile (deleted, suspended or upgraded it). What was written for this upgrade goes.
                if (reservation is not null && last!.Username.Key != reservation.Username.Key)
                {
                    await _store.ReleaseUsernameAsync(reservation, cancellationToken);
                }

                await UnbindRefusedAsync(bound, upgraded, last, identity, id, cancellationToken);
                return upgraded;
            }

            var guestIdentity = upgraded.Value.Unbinding!.Value;
            await _store.UnbindSubjectAsync(guestIdentity, id, cancellationToken);
            if (before!.Username.Key != name.Value.Key)
            {
                await ReleaseHeldAsync(before.Username, id, cancellationToken);
            }

            return await UnboundAsync(upgraded.Value, guestIdentity, cancellationToken);
        }
        finally
        {
            Writes.Release();
        }
    }

    // An identity a try bound itself, then a later try refused (#1217): the account does not list it, so its binding
    // holds nobody, and the account does not keep it as Unbinding either; it goes now, or nothing would find it. The
    // account is read once more just before: another process's link may have taken the binding up since the refusal,
    // writing only the account. What is left is the moment between that read and the removal, which no condition of
    // the store can close, since taking a binding up writes nothing to it.
    private async Task UnbindRefusedAsync(bool bound, Result<Account> written, Account? last, ExternalIdentity identity, AccountId id, CancellationToken cancellationToken)
    {
        if (bound && written.IsFailure && last?.Identities.Contains(identity) != true
            && (await _store.FindAsync(id, cancellationToken))?.Identities.Contains(identity) != true)
        {
            await _store.UnbindSubjectAsync(identity, id, cancellationToken);
        }
    }

    // Frees the name as it is held for the account now: a release names the very reservation it frees (#1227).
    private async Task ReleaseHeldAsync(Username name, AccountId id, CancellationToken cancellationToken)
    {
        if (await _store.FindUsernameAsync(name, cancellationToken) is { } held && held.Account == id)
        {
            await _store.ReleaseUsernameAsync(held, cancellationToken);
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
            var raised = await WriteAsync(id, account => Result.Success(account with { Generation = account.Generation + 1 }), cancellationToken);
            return raised.IsSuccess ? raised.Value : null;
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
            return await WriteAsync(id, change, cancellationToken);
        }
        finally
        {
            Writes.Release();
        }
    }

    private Task<Result<Account>> WriteAsync(AccountId id, Func<Account, Result<Account>> change, CancellationToken cancellationToken) =>
        WriteAsync(id, account => Task.FromResult(change(account)), cancellationToken);

    /// <summary>
    /// Reads the account, asks what it becomes, and writes that over the version read (#1217); while another process
    /// wrote it first, reads it again and asks again, so a change is decided on the account as it is now, never as it
    /// was. A failure, or a change that returns the account unchanged, writes nothing; an account gone is <see cref="AccountErrors.NotFound" />, and
    /// nothing is written back. Every try lost, it throws, as a store out of service does.
    /// </summary>
    private async Task<Result<Account>> WriteAsync(AccountId id, Func<Account, Task<Result<Account>>> change, CancellationToken cancellationToken)
    {
        for (var tried = 0; tried < Tries; tried++)
        {
            if (await _store.ReadAsync(id, cancellationToken) is not { } read)
            {
                return Result.Failure<Account>(AccountErrors.NotFound);
            }

            var changed = await change(read.Account);
            if (changed.IsFailure
                || ReferenceEquals(changed.Value, read.Account)
                || await _store.SaveIfUnchangedAsync(changed.Value, read.Version, cancellationToken))
            {
                return changed;
            }
        }

        throw new IOException($"Account {id} lost {Tries} writes in a row to another process.");
    }

    /// <summary>
    /// Removes the binding a removal that stopped between its writes left (#1216), and answers the account without it,
    /// to be written with whatever change comes next: the account no longer lists the identity, so it reaches nothing.
    /// </summary>
    private async Task<Account> FinishUnbindingAsync(Account account, CancellationToken cancellationToken)
    {
        if (account.Unbinding is not { } pending)
        {
            return account;
        }

        await _store.UnbindSubjectAsync(pending, account.Id, cancellationToken);
        return account with { Unbinding = null };
    }

    /// <summary>
    /// Writes the account again without <see cref="Account.Unbinding" />, after the binding has gone. If the account went
    /// meanwhile, the caller is answered what it wrote: what it asked for is done. A store that fails here throws, as
    /// anywhere, and the account keeps <see cref="Account.Unbinding" /> until the next link, removal or deletion.
    /// </summary>
    private async Task<Result<Account>> UnboundAsync(Account written, ExternalIdentity identity, CancellationToken cancellationToken)
    {
        var cleared = await WriteAsync(
            written.Id,
            account => Result.Success(account.Unbinding == identity ? account with { Unbinding = null } : account),
            cancellationToken);
        return cleared.IsSuccess ? cleared : Result.Success(written);
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
            var bound = false;
            Account? last = null;
            var linked = await WriteAsync(
                id,
                async account =>
                {
                    last = account;
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

                    var finished = await FinishUnbindingAsync(account, cancellationToken);
                    if (finished.Identities.Contains(identity))
                    {
                        return Result.Success(finished);
                    }

                    if (finished.Identities.ByProvider(identity.Provider) is not null)
                    {
                        return Result.Failure<Account>(AccountErrors.ProviderLinked);
                    }

                    var binding = await BindAsync(identity, id, cancellationToken);
                    bound |= binding.Made;
                    if (!binding.Bound)
                    {
                        return Result.Failure<Account>(AccountErrors.IdentityTaken);
                    }

                    return Result.Success(finished with { Identities = finished.Identities.With(identity) });
                },
                cancellationToken);
            await UnbindRefusedAsync(bound, linked, last, identity, id, cancellationToken);
            return linked;
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>
    /// Removes a way into the account (ADR 0204); the last one stays, since deleting the account is how that goes.
    /// The removal raises the generation in the same write, so whoever signed in through the identity removed is
    /// signed out with every other session. The account is written first, keeping the identity as
    /// <see cref="Account.Unbinding" />, then the binding removed, then the account written without it. If the removal
    /// stops between the first two, the binding reaches nothing, since the account no longer lists it, and no other
    /// account can take the identity while this one lists it. Removing it again finishes the job, as does the next
    /// link, removal or deletion of the account (#1216).
    /// <paramref name="generation" />, when given, is the asking session's: one ended meanwhile removes nothing.
    /// </summary>
    public async Task<Result<Account>> UnlinkAsync(AccountId id, ExternalIdentity identity, int? generation = null, CancellationToken cancellationToken = default)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            var unlinked = await WriteAsync(
                id,
                async account =>
                {
                    if (generation is { } asked && asked != account.Generation)
                    {
                        return Result.Failure<Account>(AccountErrors.SessionEnded);
                    }

                    var finished = await FinishUnbindingAsync(account, cancellationToken);
                    if (account.Unbinding == identity)
                    {
                        // This very removal stopped between its writes: its binding is gone now.
                        return Result.Success(finished);
                    }

                    var remaining = finished.Identities.Without(identity);
                    if (remaining.IsFailure)
                    {
                        return remaining.Error == AccountErrors.IdentityNotLinked && await _store.UnbindSubjectAsync(identity, id, cancellationToken)
                            ? Result.Success(finished)
                            : Result.Failure<Account>(remaining.Error);
                    }

                    return Result.Success(finished with { Identities = remaining.Value, Generation = finished.Generation + 1, Unbinding = identity });
                },
                cancellationToken);
            if (unlinked.IsFailure || unlinked.Value.Unbinding != identity)
            {
                return unlinked;
            }

            await _store.UnbindSubjectAsync(identity, id, cancellationToken);
            return await UnboundAsync(unlinked.Value, identity, cancellationToken);
        }
        finally
        {
            Writes.Release();
        }
    }

    // Binds the identity to the account: Bound when it now names it, already or once a binding that holds nobody is
    // taken back, false when another account holds it; Made when this call wrote the binding, so a refusal later undoes
    // only what it wrote, never a binding another link in flight made (#1217).
    private async Task<(bool Bound, bool Made)> BindAsync(ExternalIdentity identity, AccountId id, CancellationToken cancellationToken)
    {
        if (await _store.TryBindSubjectAsync(identity, id, cancellationToken))
        {
            return (true, true);
        }

        var holder = await _store.FindBySubjectAsync(identity, cancellationToken);
        if (holder == id)
        {
            return (true, false);
        }

        if (holder is { } other)
        {
            if (!await HoldsNobodyAsync(identity, other, cancellationToken))
            {
                return (false, false);
            }

            await _store.UnbindSubjectAsync(identity, other, cancellationToken);
        }

        return await _store.TryBindSubjectAsync(identity, id, cancellationToken)
            ? (true, true)
            : (await _store.FindBySubjectAsync(identity, cancellationToken) == id, false);
    }

    /// <summary>
    /// Removes the account (ADR 0122): its sign-ins first, so nobody signs in to it again, then the account, then its
    /// name, which another player may then take. The same sign-ins can register afresh afterwards, under a new id. A
    /// binding a removal left (<see cref="Account.Unbinding" />) goes with them. The account is removed only if it is
    /// unchanged since it was read; a way in that another process added meanwhile is read again and removed too
    /// (#1217). False when there is no such account.
    /// </summary>
    public Task<bool> DeleteAsync(AccountId id, CancellationToken cancellationToken = default) => DeleteAsync(id, still: null, cancellationToken);

    /// <param name="still">What the account must still be, checked on every read: the sweep of idle guests deletes a guest, never the player's account it was upgraded to meanwhile by another process. False when it no longer is.</param>
    public async Task<bool> DeleteAsync(AccountId id, Func<Account, bool>? still, CancellationToken cancellationToken = default)
    {
        await Writes.WaitAsync(cancellationToken);
        try
        {
            for (var tried = 0; tried < Tries; tried++)
            {
                if (await _store.ReadAsync(id, cancellationToken) is not { Account: var account } read || still?.Invoke(account) == false)
                {
                    return false;
                }

                foreach (var identity in account.Unbinding is { } pending ? [.. account.Identities.All, pending] : account.Identities.All)
                {
                    await _store.UnbindSubjectAsync(identity, id, cancellationToken);
                }

                if (await _store.DeleteIfUnchangedAsync(id, read.Version, cancellationToken))
                {
                    await ReleaseHeldAsync(account.Username, id, cancellationToken);
                    return true;
                }
            }

            throw new IOException($"Account {id} lost {Tries} removals in a row to another process.");
        }
        finally
        {
            Writes.Release();
        }
    }

    /// <summary>
    /// Gives the account another name (ADR 0174), checked as a name chosen at registration is: the new name is held
    /// first, then the account written, then the old name freed for another player. A change of case alone keeps the
    /// name it holds. A rename that stopped between its writes leaves a name held for an account under another, freed
    /// an hour on (<see cref="AbandonedAfter" />). Sessions are left alone: they name the account, not its name.
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
            UsernameReservation? reservation = null;
            Account? before = null;
            var renamed = await WriteAsync(
                id,
                async account =>
                {
                    before = account;
                    if (name.Value.Key == account.Username.Key)
                    {
                        return Result.Success(account with { Username = name.Value });
                    }

                    reservation ??= new UsernameReservation(name.Value, id, _clock.GetUtcNow());
                    return await ReserveAsync(reservation, account.Identities.All[0], cancellationToken) is { } held && held.Account == id
                        ? Result.Success(account with { Username = name.Value })
                        : Result.Failure<Account>(AccountErrors.UsernameTaken);
                },
                cancellationToken);
            if (renamed.IsFailure)
            {
                if (renamed.Error == AccountErrors.NotFound && reservation is not null)
                {
                    await _store.ReleaseUsernameAsync(reservation, cancellationToken);
                }

                return renamed;
            }

            if (before!.Username.Key != name.Value.Key)
            {
                await ReleaseHeldAsync(before.Username, id, cancellationToken);
            }

            return renamed;
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
            var read = await _store.ReadAsync(holder.Account, cancellationToken);
            var account = read?.Account;
            if (account is not null && account.Identities.Contains(identity))
            {
                // Its own name held an hour for it under another one would be free to anybody (AbandonedAsync): held
                // afresh before it is taken up, so a registration cannot free it under the write that names it.
                if (account.Username.Key == holder.Username.Key || _clock.GetUtcNow() - holder.ReservedAt < AbandonedAfter)
                {
                    return holder;
                }

                await _store.ReleaseUsernameAsync(holder, cancellationToken);
                return await _store.TryReserveUsernameAsync(reservation with { Account = holder.Account }, cancellationToken)
                    ? reservation with { Account = holder.Account }
                    : null;
            }

            if (!await AbandonedAsync(holder, account, cancellationToken))
            {
                return null;
            }

            await _store.ReleaseUsernameAsync(holder, cancellationToken);
            // An account under the name it holds was never bound: a registration that stopped. One under another name
            // is somebody's, and keeps its own. It goes only as it was read.
            if (read is not null && read.Account.Username.Key == holder.Username.Key)
            {
                await _store.DeleteIfUnchangedAsync(read.Account.Id, read.Version, cancellationToken);
            }
        }

        return await _store.TryReserveUsernameAsync(reservation, cancellationToken) ? reservation : null;
    }

    private async Task<bool> AbandonedAsync(UsernameReservation holder, CancellationToken cancellationToken) =>
        await AbandonedAsync(holder, await _store.FindAsync(holder.Account, cancellationToken), cancellationToken);

    /// <summary>
    /// A name is abandoned when it has been held an hour for an account that was never written, or for one none of
    /// whose identities is bound to it: both are a registration that stopped between its writes. A name is also
    /// abandoned when it has been held an hour for an account that now has another name. That happens when a rename or
    /// an upgrade stopped between its writes, when an upgrade was asked again with another name (#1227), or when the
    /// old name was never freed.
    /// </summary>
    private async Task<bool> AbandonedAsync(UsernameReservation holder, Account? account, CancellationToken cancellationToken) =>
        _clock.GetUtcNow() - holder.ReservedAt >= AbandonedAfter
        && (account is null || account.Username.Key != holder.Username.Key || !await AnyBoundAsync(account, cancellationToken));

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
