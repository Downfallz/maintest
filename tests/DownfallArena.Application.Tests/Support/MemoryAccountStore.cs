using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Support;

/// <summary>The account store's contract in memory; <see cref="RefuseNextBinding" /> plays a registration that lost a race.</summary>
internal sealed class MemoryAccountStore : IAccountStore
{
    public Dictionary<AccountId, Account> Accounts { get; } = [];

    public Dictionary<ExternalIdentity, AccountId> Subjects { get; } = [];

    public Dictionary<string, UsernameReservation> Names { get; } = [];

    public bool RefuseNextBinding { get; set; }

    public Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Accounts.GetValueOrDefault(id));

    public Task SaveAsync(Account account, CancellationToken cancellationToken = default)
    {
        Accounts[account.Id] = account;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(AccountId id, CancellationToken cancellationToken = default) => Task.FromResult(Accounts.Remove(id));

    public Task<AccountId?> FindBySubjectAsync(ExternalIdentity identity, CancellationToken cancellationToken = default) =>
        Task.FromResult(Subjects.TryGetValue(identity, out var id) ? id : (AccountId?)null);

    public Task<bool> TryBindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default)
    {
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
}
