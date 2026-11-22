using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Where an account's game settings are kept (ADR 0140): one document per account, read and written whole,
/// beside the account. Its Tutorials done and skipped (ADR 0187) are a second document beside the first: an
/// account that skipped a Tutorial has still chosen no settings. What pops up on its devices (ADR 0202) is a third. Owned by Application, implemented by Infrastructure.
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

    /// <summary>What pops up on the account's devices (ADR 0202); <see cref="NoticePreferences.Default" /> for an account that never chose.</summary>
    Task<NoticePreferences> GetNoticesAsync(AccountId account, CancellationToken cancellationToken = default);

    /// <summary>Replaces what pops up on the account's devices with these.</summary>
    Task SaveNoticesAsync(AccountId account, NoticePreferences notices, CancellationToken cancellationToken = default);

    /// <summary>Removes the account's settings, its Tutorials done and skipped, and what pops up: the account going.</summary>
    Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default);
}
