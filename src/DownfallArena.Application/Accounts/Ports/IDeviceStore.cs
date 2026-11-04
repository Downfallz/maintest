using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Where an account's devices are kept (friends plan, stage 1): one document per account, read and written whole.
/// The rules -- how many, which one goes -- are <see cref="AccountDevices" />'s; this only keeps the list. Owned by
/// Application, implemented by Infrastructure beside the account store.
/// </summary>
public interface IDeviceStore
{
    /// <summary>The account's devices, oldest first; empty for an account that registered none.</summary>
    Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default);

    /// <summary>Replaces the account's list with this one; an empty list leaves nothing behind.</summary>
    Task SaveAsync(AccountId account, IReadOnlyList<PushDevice> devices, CancellationToken cancellationToken = default);

    /// <summary>Removes every device of the account: signing out everywhere, or the account going.</summary>
    Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default);
}
