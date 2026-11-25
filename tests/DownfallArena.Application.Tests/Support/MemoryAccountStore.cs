using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// The account store's contract in memory; <see cref="RefuseNextBinding" /> plays a registration that lost a race,
/// <see cref="FailNextBinding" /> one whose host stopped before it bound its sign-in, <see cref="BeforeNextBinding" /> another
/// writer getting there first, <see cref="BeforeNextAccountWrite" /> another host writing the account between a read and a
/// write, <see cref="FailNextAccountWrite" /> a host that stopped before it wrote the account, <see cref="BeforeEveryAccountWrite" />
/// another host that always gets there first, <see cref="BeforeNextUnbinding" /> another host landing just before a binding goes.
/// </summary>
internal sealed class MemoryAccountStore : IAccountStore
{
    public Dictionary<AccountId, Account> Accounts { get; } = [];

    public Dictionary<ExternalIdentity, AccountId> Subjects { get; } = [];

    public Dictionary<string, UsernameReservation> Names { get; } = [];

    // A binding's version, changed by every write of it; one a test set in Subjects directly gets one when first read.
    private readonly Dictionary<ExternalIdentity, string> _subjectVersions = [];

    // A version names the very record read: any write after it, even of an equal record, is another one.
    private readonly Dictionary<string, Account> _versions = [];

    /// <summary>Runs once just before the next write or removal of an account: another host landing between a read and it.</summary>
    public Action? BeforeNextAccountWrite { get; set; }

    /// <summary>Runs before every write or removal of an account: another host that writes the account first, every time.</summary>
    public Action? BeforeEveryAccountWrite { get; set; }

    /// <summary>Runs once just before the next removal of a binding, conditional or not.</summary>
    public Action? BeforeNextUnbinding { get; set; }

    /// <summary>Makes the next write of an account throw: a host that stopped before it.</summary>
    public bool FailNextAccountWrite { get; set; }

    /// <summary>How many writes of an account were refused because another came first.</summary>
    public int RefusedWrites { get; private set; }

    public bool RefuseNextBinding { get; set; }

    public bool FailNextBinding { get; set; }

    /// <summary>Makes the next removal of a binding throw: a host that stopped before it.</summary>
    public bool FailNextUnbinding { get; set; }

    /// <summary>Runs once just before the next binding: somebody else's write landing between a check and the bind.</summary>
    public Action? BeforeNextBinding { get; set; }

    /// <summary>Lets every read of an account yield first, as a store across a network does, so racing callers interleave.</summary>
    public bool SlowReads { get; set; }

    /// <summary>Lets every write of an account wait first, so a write can land after something read before it.</summary>
    public bool SlowWrites { get; set; }

    /// <summary>Runs once just before the read of an account numbered <see cref="AccountReadsBeforeHook" /> from now (0 is the next).</summary>
    public Action? BeforeAccountRead { get; set; }

    public int AccountReadsBeforeHook { get; set; }

    public async Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        if (BeforeAccountRead is { } before && AccountReadsBeforeHook-- == 0)
        {
            BeforeAccountRead = null;
            before();
        }

        if (SlowReads)
        {
            await Task.Delay(1, cancellationToken);
        }

        lock (Accounts)
        {
            return Accounts.GetValueOrDefault(id);
        }
    }

    public async Task<AccountRead?> ReadAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(id, cancellationToken) is not { } account)
        {
            return null;
        }

        var version = Guid.NewGuid().ToString("N");
        lock (Accounts)
        {
            _versions[version] = account;
        }

        return new AccountRead(account, version);
    }

    public async Task<bool> SaveIfUnchangedAsync(Account account, string version, CancellationToken cancellationToken = default)
    {
        await BeforeWriteAsync(cancellationToken);
        lock (Accounts)
        {
            if (!Unchanged(account.Id, version))
            {
                RefusedWrites++;
                return false;
            }

            Accounts[account.Id] = account;
            return true;
        }
    }

    public Task<bool> DeleteIfUnchangedAsync(AccountId id, string version, CancellationToken cancellationToken = default)
    {
        RunBeforeNextAccountWrite();
        lock (Accounts)
        {
            if (!Unchanged(id, version))
            {
                RefusedWrites++;
                return Task.FromResult(false);
            }

            return Task.FromResult(Accounts.Remove(id));
        }
    }

    private bool Unchanged(AccountId id, string version) =>
        _versions.TryGetValue(version, out var read) && Accounts.TryGetValue(id, out var current) && ReferenceEquals(read, current);

    private async Task BeforeWriteAsync(CancellationToken cancellationToken)
    {
        if (SlowWrites)
        {
            await Task.Delay(5, cancellationToken);
        }

        if (FailNextAccountWrite)
        {
            FailNextAccountWrite = false;
            throw new IOException("The store went away.");
        }

        RunBeforeNextAccountWrite();
    }

    private void RunBeforeNextAccountWrite()
    {
        BeforeEveryAccountWrite?.Invoke();
        if (BeforeNextAccountWrite is { } before)
        {
            BeforeNextAccountWrite = null;
            before();
        }
    }

    public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (Accounts)
        {
            return Task.FromResult<IReadOnlyList<Account>>([.. Accounts.Values]);
        }
    }

    public async Task SaveAsync(Account account, CancellationToken cancellationToken = default)
    {
        await BeforeWriteAsync(cancellationToken);
        lock (Accounts)
        {
            Accounts[account.Id] = account;
        }
    }

    public Task<bool> DeleteAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        RunBeforeNextAccountWrite();
        lock (Accounts)
        {
            return Task.FromResult(Accounts.Remove(id));
        }
    }

    public Task<AccountId?> FindBySubjectAsync(ExternalIdentity identity, CancellationToken cancellationToken = default) =>
        Task.FromResult(Subjects.TryGetValue(identity, out var id) ? id : (AccountId?)null);

    public Task<SubjectRead?> ReadSubjectAsync(ExternalIdentity identity, CancellationToken cancellationToken = default) =>
        Task.FromResult(Subjects.TryGetValue(identity, out var id) ? new SubjectRead(id, SubjectVersion(identity)) : null);

    private string SubjectVersion(ExternalIdentity identity)
    {
        if (!_subjectVersions.TryGetValue(identity, out var version))
        {
            version = Guid.NewGuid().ToString("N");
            _subjectVersions[identity] = version;
        }

        return version;
    }

    private string Rewritten(ExternalIdentity identity, AccountId account)
    {
        Subjects[identity] = account;
        var version = Guid.NewGuid().ToString("N");
        _subjectVersions[identity] = version;
        return version;
    }

    /// <summary>What another host's link of the same identity to the same account does to its binding: takes it up by writing it.</summary>
    public void AdoptedElsewhere(ExternalIdentity identity) => Rewritten(identity, Subjects[identity]);

    public async Task<bool> TryBindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default) =>
        await BindSubjectAsync(identity, account, over: null, cancellationToken) is not null;

    public Task<string?> BindSubjectAsync(ExternalIdentity identity, AccountId account, string? over, CancellationToken cancellationToken = default)
    {
        if (over is not null)
        {
            return Task.FromResult(Subjects.ContainsKey(identity) && SubjectVersion(identity) == over ? Rewritten(identity, account) : null);
        }

        if (FailNextBinding)
        {
            FailNextBinding = false;
            throw new IOException("The store went away.");
        }

        if (BeforeNextBinding is { } before)
        {
            BeforeNextBinding = null;
            before();
        }

        if (RefuseNextBinding)
        {
            RefuseNextBinding = false;
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult(Subjects.ContainsKey(identity) ? null : Rewritten(identity, account));
    }

    public Task<bool> UnbindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default)
    {
        RunBeforeNextUnbinding();
        return Task.FromResult(Subjects.TryGetValue(identity, out var held) && held == account && Removed(identity));
    }

    public Task<bool> UnbindSubjectIfUnchangedAsync(ExternalIdentity identity, string version, CancellationToken cancellationToken = default)
    {
        RunBeforeNextUnbinding();
        return Task.FromResult(Subjects.ContainsKey(identity) && SubjectVersion(identity) == version && Removed(identity));
    }

    private bool Removed(ExternalIdentity identity)
    {
        _subjectVersions.Remove(identity);
        return Subjects.Remove(identity);
    }

    private void RunBeforeNextUnbinding()
    {
        if (FailNextUnbinding)
        {
            FailNextUnbinding = false;
            throw new IOException("The store went away.");
        }

        if (BeforeNextUnbinding is { } before)
        {
            BeforeNextUnbinding = null;
            before();
        }
    }

    public Task<UsernameReservation?> FindUsernameAsync(Username username, CancellationToken cancellationToken = default) =>
        Task.FromResult(Names.GetValueOrDefault(username.Key));

    public Task<bool> TryReserveUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default) =>
        Task.FromResult(Names.TryAdd(reservation.Username.Key, reservation));

    /// <summary>Runs once just before the next release of a name: another writer landing between a judgement of it and its release.</summary>
    public Action? BeforeNextNameRelease { get; set; }

    public Task<bool> ReleaseUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default)
    {
        if (BeforeNextNameRelease is { } before)
        {
            BeforeNextNameRelease = null;
            before();
        }

        return Task.FromResult(Names.TryGetValue(reservation.Username.Key, out var held)
            && held.Account == reservation.Account
            && held.ReservedAt == reservation.ReservedAt
            && Names.Remove(reservation.Username.Key));
    }

    public byte[] Key { get; } = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

    public Task<byte[]> SessionKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(Key);
}
