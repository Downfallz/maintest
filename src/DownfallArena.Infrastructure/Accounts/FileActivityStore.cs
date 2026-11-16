using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Each account's activity as one file under the accounts' directory (ADR 0174), for a laptop's table. Written beside
/// its place and moved onto it, so a reader never sees half a document.
/// </summary>
public sealed class FileActivityStore : IAccountActivityStore
{
    public FileActivityStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task<AccountActivity?> FindAsync(AccountId account, CancellationToken cancellationToken = default) =>
        await ReadAsync(FullPath(account), cancellationToken);

    public async Task<IReadOnlyList<AccountActivity>> ListAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(RootDirectory, ActivityDocuments.Prefix);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var kept = new List<AccountActivity>();
        // A staged write ends in .tmp, and a document forgotten while the directory is read is not listed.
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Where(path => path.EndsWith(".json", StringComparison.Ordinal)))
        {
            if (await ReadAsync(path, cancellationToken) is { } activity)
            {
                kept.Add(activity);
            }
        }

        return kept;
    }

    public async Task SaveAsync(AccountActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);
        var path = FullPath(activity.Account);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, ActivityDocuments.Write(activity), cancellationToken);
        File.Move(staged, path, overwrite: true);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        File.Delete(FullPath(account));
        return Task.CompletedTask;
    }

    private static async Task<AccountActivity?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return ActivityDocuments.Read(await File.ReadAllTextAsync(path, cancellationToken));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private string FullPath(AccountId account) => Path.Combine(RootDirectory, ActivityDocuments.PathOf(account));
}
