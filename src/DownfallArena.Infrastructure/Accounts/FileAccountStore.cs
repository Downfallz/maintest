using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// Accounts as files under one root, for a laptop's table. A record is written beside its place and moved onto
/// it, so a reader never sees half of one; an index entry is moved without overwriting, which is the
/// create-if-absent the registry relies on. The conditional removals read then delete, which is enough for one
/// host on one machine.
/// </summary>
public sealed class FileAccountStore : IAccountStore
{
    public FileAccountStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task<Account?> FindAsync(AccountId id, CancellationToken cancellationToken = default) =>
        await ReadAsync(AccountDocuments.AccountPath(id), cancellationToken) is { } json ? AccountDocuments.ReadAccount(json) : null;

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
        WriteAsync(AccountDocuments.SubjectPath(identity), AccountDocuments.WriteSubject(account), overwrite: false, cancellationToken);

    public async Task<bool> UnbindSubjectAsync(ExternalIdentity identity, AccountId account, CancellationToken cancellationToken = default) =>
        await FindBySubjectAsync(identity, cancellationToken) == account && Delete(AccountDocuments.SubjectPath(identity));

    public async Task<UsernameReservation?> FindUsernameAsync(Username username, CancellationToken cancellationToken = default) =>
        await ReadAsync(AccountDocuments.NamePath(username), cancellationToken) is { } json ? AccountDocuments.ReadName(json) : null;

    public Task<bool> TryReserveUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return WriteAsync(AccountDocuments.NamePath(reservation.Username), AccountDocuments.Write(reservation), overwrite: false, cancellationToken);
    }

    public async Task<bool> ReleaseUsernameAsync(UsernameReservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return (await FindUsernameAsync(reservation.Username, cancellationToken))?.Account == reservation.Account
            && Delete(AccountDocuments.NamePath(reservation.Username));
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
