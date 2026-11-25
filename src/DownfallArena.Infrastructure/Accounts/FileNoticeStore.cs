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
    private readonly TextsRead<AccountId> _read;

    public FileNoticeStore(string rootDirectory)
        : this(rootDirectory, TimeProvider.System)
    {
    }

    /// <param name="time">What lets go of a text kept past its lifetime; a test's own clock.</param>
    internal FileNoticeStore(string rootDirectory, TimeProvider time)
    {
        _read = new TextsRead<AccountId>(time);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    /// <summary>How many texts this store keeps now (#1260).</summary>
    internal int Kept => _read.Count;

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
        return await _read.WriteAsync(
            account,
            version,
            entries.Count == 0 ? null : before => NoticeDocuments.Write(entries, before),
            token => ConditionalFile.ReadAsync(FullPath(account), token),
            (text, token) => ConditionalFile.WriteAsync(FullPath(account), text, version, token),
            cancellationToken);
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
        _read.ForgetAsync(account, () => ConditionalFile.DeleteAsync(FullPath(account), cancellationToken));

    private string FullPath(AccountId account) => Path.Combine(RootDirectory, NoticeDocuments.PathOf(account));
}
