using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Where accounts are kept (ADR 0122): the accounts themselves, and two indexes -- by sign-in and by name -- whose
/// entries are created only when absent, which is what makes a name and a sign-in unique without a lock.
/// Owned by Application, implemented by Infrastructure (a directory on a laptop, a blob container on Azure).
/// </summary>
public interface IAccountStore
{
    Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every account, in no particular order: what the operator's list of players reads (ADR 0174). It reads each
    /// one, which is what a host of tens to hundreds of accounts affords.
    /// </summary>
    Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the account whole, replacing what was there.</summary>
    Task SaveAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>Removes the account record; its indexes are the caller's to release. False when there was none.</summary>
    Task<bool> DeleteAsync(AccountId id, CancellationToken cancellationToken = default);

    Task<AccountId?> FindBySubjectAsync(ExternalIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>Binds the sign-in to the account if nothing holds it yet; false when something already does.</summary>
    Task<bool> TryBindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default);

    /// <summary>Removes the binding if it still names that account; false otherwise.</summary>
    Task<bool> UnbindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default);

    /// <summary>Who holds the name, compared by <see cref="Username.Key" />, or null.</summary>
    Task<UsernameReservation?> FindUsernameAsync(Username username, CancellationToken cancellationToken = default);

    /// <summary>Holds the name for the account if nobody holds it yet; false when somebody already does.</summary>
    Task<bool> TryReserveUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default);

    /// <summary>Frees the name if it is still held for that reservation's account; false otherwise.</summary>
    Task<bool> ReleaseUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default);

    /// <summary>
    /// The key the host signs its sessions with, made the first time it is asked for and the same afterwards, on
    /// this host and on the next. It is never typed by anyone; replacing it signs everybody out.
    /// </summary>
    Task<byte[]> SessionKeyAsync(CancellationToken cancellationToken = default);
}
