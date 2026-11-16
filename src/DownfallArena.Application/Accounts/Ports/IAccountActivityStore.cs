using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Where each account's activity is kept (ADR 0174): one document an account, beside the account, read and written
/// whole. Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IAccountActivityStore
{
    /// <summary>The account's activity, or null for an account none was kept for.</summary>
    Task<AccountActivity?> FindAsync(AccountId account, CancellationToken cancellationToken = default);

    /// <summary>Every account's activity, in no particular order: what the operator's list of players reads once.</summary>
    Task<IReadOnlyList<AccountActivity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the account's activity with this.</summary>
    Task SaveAsync(AccountActivity activity, CancellationToken cancellationToken = default);

    /// <summary>Removes the account's activity: the account going.</summary>
    Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default);
}
