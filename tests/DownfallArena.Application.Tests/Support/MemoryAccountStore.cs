using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// The account store's contract in memory; <see cref="RefuseNextBinding" /> plays a registration that lost a race,
/// <see cref="FailNextBinding" /> one whose host stopped before it bound its sign-in.
/// </summary>
internal sealed class MemoryAccountStore : IAccountStore
{
    public Dictionary<AccountId, Account> Accounts { get; } = [];

    public Dictionary<ExternalIdentity, AccountId> Subjects { get; } = [];

    public Dictionary<string, UsernameReservation> Names { get; } = [];

    public bool RefuseNextBinding { get; set; }

    public bool FailNextBinding { get; set; }

    /// <summary>Lets every read of an account yield first, as a store across a network does, so racing callers interleave.</summary>
    public bool SlowReads { get; set; }

    /// <summary>Lets every write of an account wait first, so a write can land after something read before it.</summary>
    public bool SlowWrites { get; set; }

    public async Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        if (SlowReads)
        {
            await Task.Delay(1, cancellationToken);
        }

        lock (Accounts)
        {
            return Accounts.GetValueOrDefault(id);
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
        if (SlowWrites)
        {
            await Task.Delay(5, cancellationToken);
        }

        lock (Accounts)
        {
            Accounts[account.Id] = account;
        }
    }

    public Task<bool> DeleteAsync(AccountId id, CancellationToken cancellationToken = default)
    {
        lock (Accounts)
        {
            return Task.FromResult(Accounts.Remove(id));
        }
    }

    public Task<AccountId?> FindBySubjectAsync(ExternalIdentity identity, CancellationToken cancellationToken = default) =>
        Task.FromResult(Subjects.TryGetValue(identity, out var id) ? id : (AccountId?)null);

    public Task<bool> TryBindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default)
    {
        if (FailNextBinding)
        {
            FailNextBinding = false;
            throw new IOException("The store went away.");
        }

        if (RefuseNextBinding)
        {
            RefuseNextBinding = false;
            return Task.FromResult(false);
        }

        return Task.FromResult(Subjects.TryAdd(identity, account));
    }

    public Task<bool> UnbindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default) =>
        Task.FromResult(Subjects.TryGetValue(identity, out var held) && held == account && Subjects.Remove(identity));

    public Task<UsernameReservation?> FindUsernameAsync(Username username, CancellationToken cancellationToken = default) =>
        Task.FromResult(Names.GetValueOrDefault(username.Key));

    public Task<bool> TryReserveUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default) =>
        Task.FromResult(Names.TryAdd(reservation.Username.Key, reservation));

    public Task<bool> ReleaseUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default) =>
        Task.FromResult(Names.TryGetValue(reservation.Username.Key, out var held) && held.Account == reservation.Account && Names.Remove(reservation.Username.Key));

    public byte[] Key { get; } = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

    public Task<byte[]> SessionKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(Key);
}
