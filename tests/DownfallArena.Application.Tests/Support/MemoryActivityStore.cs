using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Support;

/// <summary>The activity store's contract in memory, counting its writes.</summary>
internal sealed class MemoryActivityStore : IAccountActivityStore
{
    public Dictionary<AccountId, AccountActivity> Kept { get; } = [];

    public int Writes { get; private set; }

    public Task<AccountActivity?> FindAsync(AccountId account, CancellationToken cancellationToken = default) =>
        Task.FromResult(Kept.GetValueOrDefault(account));

    public Task<IReadOnlyList<AccountActivity>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AccountActivity>>([.. Kept.Values]);

    public Task SaveAsync(AccountActivity activity, CancellationToken cancellationToken = default)
    {
        Kept[activity.Account] = activity;
        Writes++;
        return Task.CompletedTask;
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        Kept.Remove(account);
        return Task.CompletedTask;
    }
}
