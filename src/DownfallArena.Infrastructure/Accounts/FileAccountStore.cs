using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Accounts as files under one root, for a laptop's table. A record is written beside its place and moved onto
/// it, so a reader never sees half of one; an index entry is moved without overwriting, which is the
/// create-if-absent the registry relies on. A conditional removal reads then deletes, so it and every index write
/// take one lock: otherwise a name reserved between the read and the delete would be the one deleted. The lock is
/// the process's, which is what a laptop's table is: one host over one directory.
/// </summary>
public sealed class FileAccountStore : IAccountStore
{
    private static readonly SemaphoreSlim Indexes = new(1, 1);

    public FileAccountStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default) =>
        await ReadAsync(AccountDocuments.AccountPath(id), cancellationToken) is { } json ? AccountDocuments.ReadAccount(json) : null;

    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default)
    {
        var directory = FullPath(AccountDocuments.AccountsPrefix);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var accounts = new List<Account>();
        // A staged write ends in .tmp, and an account deleted while the directory is read is not listed.
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Where(path => path.EndsWith(".json", StringComparison.Ordinal)))
        {
            if (await ReadAsync(Path.GetRelativePath(RootDirectory, path), cancellationToken) is { } json)
            {
                accounts.Add(AccountDocuments.ReadAccount(json));
            }
        }

        return accounts;
    }

    public async Task SaveAsync(Account account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await WriteAsync(AccountDocuments.AccountPath(account.Id), AccountDocuments.Write(account), overwrite: true, cancellationToken);
    }

    public Task<bool> DeleteAsync(AccountId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Delete(AccountDocuments.AccountPath(id)));

    public async Task<AccountId?> FindBySubjectAsync(ExternalIdentity identity, CancellationToken cancellationToken = default) =>
        await ReadAsync(AccountDocuments.SubjectPath(identity), cancellationToken) is { } json ? AccountDocuments.ReadSubject(json) : null;

    public Task<bool> TryBindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default) =>
        ExclusivelyAsync(() => WriteAsync(AccountDocuments.SubjectPath(identity), AccountDocuments.WriteSubject(account), overwrite: false, cancellationToken), cancellationToken);

    public Task<bool> UnbindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default) =>
        ExclusivelyAsync(async () => await FindBySubjectAsync(identity, cancellationToken) == account && Delete(AccountDocuments.SubjectPath(identity)), cancellationToken);

    public async Task<UsernameReservation?> FindUsernameAsync(Username username, CancellationToken cancellationToken = default) =>
        await ReadAsync(AccountDocuments.NamePath(username), cancellationToken) is { } json ? AccountDocuments.ReadName(json) : null;

    public Task<bool> TryReserveUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return ExclusivelyAsync(() => WriteAsync(AccountDocuments.NamePath(reservation.Username), AccountDocuments.Write(reservation), overwrite: false, cancellationToken), cancellationToken);
    }

    public Task<bool> ReleaseUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return ExclusivelyAsync(
            async () => (await FindUsernameAsync(reservation.Username, cancellationToken))?.Account == reservation.Account
                && Delete(AccountDocuments.NamePath(reservation.Username)),
            cancellationToken);
    }

    public async Task<byte[]> SessionKeyAsync(CancellationToken cancellationToken = default)
    {
        await ExclusivelyAsync(async () => await ReadAsync(AccountDocuments.SessionKeyPath, cancellationToken) is null
            && await WriteAsync(AccountDocuments.SessionKeyPath, AccountDocuments.NewSessionKey(), overwrite: false, cancellationToken), cancellationToken);
        return AccountDocuments.ReadSessionKey((await ReadAsync(AccountDocuments.SessionKeyPath, cancellationToken))!);
    }

    private static async Task<bool> ExclusivelyAsync(Func<Task<bool>> change, CancellationToken cancellationToken)
    {
        await Indexes.WaitAsync(cancellationToken);
        try
        {
            return await change();
        }
        finally
        {
            Indexes.Release();
        }
    }

    private string FullPath(string relative) => Path.Combine(RootDirectory, relative);

    private async Task<string?> ReadAsync(string relative, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(FullPath(relative), cancellationToken);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private async Task<bool> WriteAsync(string relative, string json, bool overwrite, CancellationToken cancellationToken)
    {
        var path = FullPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, json, cancellationToken);
        try
        {
            if (!overwrite && File.Exists(path))
            {
                return false;
            }

            File.Move(staged, path, overwrite);
            return true;
        }
        catch (IOException) when (!overwrite && File.Exists(path))
        {
            return false;
        }
        finally
        {
            File.Delete(staged);
        }
    }

    private bool Delete(string relative)
    {
        var path = FullPath(relative);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }
}
