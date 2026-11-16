using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Where an account's game settings are kept (ADR 0140): one document per account, read and written whole,
/// beside the account. Its Tutorials done and skipped (ADR 0187) are a second document beside the first: an
/// account that skipped a Tutorial has still chosen no settings. Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IPreferenceStore
{
    /// <summary>The account's settings, or null for an account that never saved any.</summary>
    Task<GamePreferences?> GetAsync(AccountId account, CancellationToken cancellationToken = default);

    /// <summary>Replaces the account's settings with these.</summary>
    Task SaveAsync(AccountId account, GamePreferences preferences, CancellationToken cancellationToken = default);

    /// <summary>The Tutorials the account finished and skipped; <see cref="TutorialProgress.None" /> for an account that kept none.</summary>
    Task<TutorialProgress> GetTutorialsAsync(AccountId account, CancellationToken cancellationToken = default);

    /// <summary>Replaces the account's Tutorials done and skipped with these.</summary>
    Task SaveTutorialsAsync(AccountId account, TutorialProgress tutorials, CancellationToken cancellationToken = default);

    /// <summary>Removes the account's settings and its Tutorials done and skipped: the account going.</summary>
    Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default);
}
