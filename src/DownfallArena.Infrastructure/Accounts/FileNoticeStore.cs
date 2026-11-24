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
    private readonly TextsRead<AccountId> _read = new();

    public FileNoticeStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async Task<NoticeLogRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        var (text, version, written) = await ConditionalFile.ReadWrittenAsync(FullPath(account), cancellationToken);
        _read.Remember(account, text, version);
        return new NoticeLogRead(text is null ? [] : NoticeDocuments.Read(text), version) { Written = written };
    }

    /// <summary>
    /// Writes back the fields this revision does not know (#1211), from the text read at that version: kept from this
    /// store's read, or read again (#1232). A write over a version that has moved since is refused: without being
    /// tried when the document was read again, by its condition when the text was kept.
    /// </summary>
    public async Task<bool> WriteAsync(AccountId account, IReadOnlyList<NoticeEntry> entries, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            _read.Forget(account);
        }

        string? before = null;
        if (version is not null && entries.Count > 0)
        {
            (var current, before) = await _read.AtAsync(account, version, token => ConditionalFile.ReadAsync(FullPath(account), token), cancellationToken);
            if (!current)
            {
                return false;
            }
        }

        var written = await ConditionalFile.WriteAsync(FullPath(account), entries.Count == 0 ? null : NoticeDocuments.Write(entries, before), version, cancellationToken);
        if (entries.Count == 0)
        {
            _read.Forget(account);
        }

        return written;
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

    public async Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        _read.Forget(account);
        await ConditionalFile.DeleteAsync(FullPath(account), cancellationToken);
        _read.Forget(account);
    }

    private string FullPath(AccountId account) => Path.Combine(RootDirectory, NoticeDocuments.PathOf(account));
}
