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

    public async Task<GamePreferences?> GetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        try
        {
            return PreferenceDocuments.Read(await File.ReadAllTextAsync(FullPath(account), cancellationToken));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public async Task SaveAsync(AccountId account, GamePreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var path = FullPath(account);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, PreferenceDocuments.Write(preferences), cancellationToken);
        File.Move(staged, path, overwrite: true);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        File.Delete(FullPath(account));
        return Task.CompletedTask;
    }

    private string FullPath(AccountId account) => Path.Combine(RootDirectory, PreferenceDocuments.PathOf(account));
}
