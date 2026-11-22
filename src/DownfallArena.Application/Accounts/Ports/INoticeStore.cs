using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Where an account's notice log is kept (ADR 0201): one document an account, read with its version and written only
/// over the version read. The API, the worker and the hourly job all write it, so no lock of one process protects it.
/// The rules -- how many, how long, what replaces what -- are <see cref="AccountNoticeLog" />'s. Owned by Application,
/// implemented by Infrastructure beside the devices.
/// </summary>
public interface INoticeStore
{
    /// <summary>The account's entries as stored, and their version; none and no version for a log never written.</summary>
    Task<NoticeLogRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the entries only if the log is still at <paramref name="version" /> (none: only if there is no log yet).
    /// False when another write came first; an empty list leaves nothing behind.
    /// </summary>
    Task<bool> WriteAsync(AccountId account, IReadOnlyList<NoticeEntry> entries, string? version, CancellationToken cancellationToken = default);

    /// <summary>Removes the account's log: the account is being deleted.</summary>
    Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default);
}
