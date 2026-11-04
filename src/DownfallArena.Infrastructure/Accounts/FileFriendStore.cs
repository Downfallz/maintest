using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// An account's friendships as one file under the accounts' directory, for a laptop's table (ADR 0129). Written
/// beside its place and moved onto it, so a reader never sees half a list; an empty list is no file.
/// </summary>
public sealed class FileFriendStore : IFriendStore
{
    public FileFriendStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task<FriendList> ListAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        try
        {
            return FriendDocuments.Read(await File.ReadAllTextAsync(FullPath(account), cancellationToken));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return FriendList.Empty;
        }
    }

    public async Task SaveAsync(AccountId account, FriendList list, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (FriendDocuments.IsEmpty(list))
        {
            await ForgetAsync(account, cancellationToken);
            return;
        }

        var path = FullPath(account);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, FriendDocuments.Write(list), cancellationToken);
        File.Move(staged, path, overwrite: true);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        File.Delete(FullPath(account));
        return Task.CompletedTask;
    }

    private string FullPath(AccountId account) => Path.Combine(RootDirectory, FriendDocuments.PathOf(account));
}
