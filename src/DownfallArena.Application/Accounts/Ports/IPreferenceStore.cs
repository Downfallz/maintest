using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Where an account's game settings are kept (ADR 0140): one document per account, read and written whole,
/// beside the account. Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IPreferenceStore
{
    /// <summary>The account's settings, or null for an account that never saved any.</summary>
    Task<GamePreferences?> GetAsync(AccountId account, CancellationToken cancellationToken = default);

    /// <summary>Replaces the account's settings with these.</summary>
    Task SaveAsync(AccountId account, GamePreferences preferences, CancellationToken cancellationToken = default);

    /// <summary>Removes the account's settings: the account going.</summary>
    Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default);
}
