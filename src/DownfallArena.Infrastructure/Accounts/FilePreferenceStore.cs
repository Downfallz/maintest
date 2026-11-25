using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// An account's game settings as one file under the accounts' directory, for a laptop's table. Written beside its
/// place and moved onto it, so a reader never sees half a document.
/// </summary>
public sealed class FilePreferenceStore : IPreferenceStore
{
    public FilePreferenceStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task<GamePreferences?> GetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        await ReadAsync(FullPath(account), cancellationToken) is { } json ? PreferenceDocuments.Read(json) : null;

    public async Task<TutorialProgress> GetTutorialsAsync(AccountId account, CancellationToken cancellationToken = default) =>
        await ReadAsync(TutorialsPath(account), cancellationToken) is { } json ? PreferenceDocuments.ReadTutorials(json) : TutorialProgress.None;

    public Task SaveTutorialsAsync(AccountId account, TutorialProgress tutorials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tutorials);
        return WriteAsync(TutorialsPath(account), PreferenceDocuments.WriteTutorials(tutorials), cancellationToken);
    }

    public async Task<NoticePreferences> GetNoticesAsync(AccountId account, CancellationToken cancellationToken = default) =>
        await ReadAsync(NoticesPath(account), cancellationToken) is { } json ? PreferenceDocuments.ReadNotices(json) : NoticePreferences.Default;

    public Task SaveNoticesAsync(AccountId account, NoticePreferences notices, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notices);
        return WriteAsync(NoticesPath(account), PreferenceDocuments.WriteNotices(notices), cancellationToken);
    }

    public async Task SaveAsync(AccountId account, GamePreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        await WriteAsync(FullPath(account), PreferenceDocuments.Write(preferences), cancellationToken);
    }

    public Task<IReadOnlyList<AccountId>> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        var folder = Path.Combine(RootDirectory, PreferenceDocuments.Prefix);
        IReadOnlyList<AccountId> accounts = Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder).Select(path => PreferenceDocuments.AccountOf(Path.GetFileName(path))).OfType<AccountId>().Distinct()]
            : [];
        return Task.FromResult(accounts);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        // An account on a host where nobody saved anything yet has no directory to delete from.
        if (Directory.Exists(Path.GetDirectoryName(FullPath(account))))
        {
            File.Delete(FullPath(account));
            File.Delete(TutorialsPath(account));
            File.Delete(NoticesPath(account));
        }

        return Task.CompletedTask;
    }

    private static async Task<string?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static async Task WriteAsync(string path, string document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, document, cancellationToken);
        File.Move(staged, path, overwrite: true);
    }

    private string FullPath(AccountId account) => Path.Combine(RootDirectory, PreferenceDocuments.PathOf(account));

    private string NoticesPath(AccountId account) => Path.Combine(RootDirectory, PreferenceDocuments.NoticesPathOf(account));

    private string TutorialsPath(AccountId account) => Path.Combine(RootDirectory, PreferenceDocuments.TutorialsPathOf(account));
}
