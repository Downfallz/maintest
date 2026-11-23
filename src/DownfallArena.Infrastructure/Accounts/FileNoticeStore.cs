using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// An account's notice log as one file under the accounts' directory, for a laptop's table (ADR 0201): written beside
/// its place and moved onto it, only over the version read; an empty log is no file.
/// </summary>
public sealed class FileNoticeStore : INoticeStore
{
    public FileNoticeStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task<NoticeLogRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        var (text, version, written) = await ConditionalFile.ReadWrittenAsync(FullPath(account), cancellationToken);
        return new NoticeLogRead(text is null ? [] : NoticeDocuments.Read(text), version) { Written = written };
    }

    public Task<bool> WriteAsync(AccountId account, IReadOnlyList<NoticeEntry> entries, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return ConditionalFile.WriteAsync(FullPath(account), entries.Count == 0 ? null : NoticeDocuments.Write(entries), version, cancellationToken);
    }

    public Task<IReadOnlyList<AccountId>> AccountsAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(RootDirectory, NoticeDocuments.Prefix);
        IReadOnlyList<AccountId> accounts = !Directory.Exists(directory)
            ? []
            : [.. Directory.EnumerateFiles(directory)
                .Select(file => NoticeDocuments.AccountOf(NoticeDocuments.Prefix + Path.GetFileName(file)))
                .OfType<AccountId>()];
        return Task.FromResult(accounts);
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        ConditionalFile.DeleteAsync(FullPath(account), cancellationToken);

    private string FullPath(AccountId account) => Path.Combine(RootDirectory, NoticeDocuments.PathOf(account));
}
