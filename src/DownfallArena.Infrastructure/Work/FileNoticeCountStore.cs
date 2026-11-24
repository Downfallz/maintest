using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Work;

/// <summary>
/// The notices' counts as one file a day in a directory of their own, for a laptop's table (N6): written beside its
/// place and moved onto it, only over the version read.
/// </summary>
public sealed class FileNoticeCountStore : INoticeCountStore
{
    private readonly TextsRead<DateOnly> _read = new();

    public FileNoticeCountStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = Path.GetFullPath(directory);
    }

    public string Directory { get; }

    public async Task<NoticeCountRead> ReadAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var (text, version) = await ConditionalFile.ReadAsync(FullPath(day), cancellationToken);
        _read.Remember(day, text, version);
        return new NoticeCountRead(text is null ? [] : NoticeCountDocuments.Read(text), version);
    }

    /// <summary>
    /// Writes back what this revision does not know of the day (#1151), from the text read at that version: kept from
    /// this store's read, or read again (#1232). A write over a version that has moved since is refused, without being tried.
    /// </summary>
    public async Task<bool> WriteAsync(DateOnly day, IReadOnlyList<NoticeCount> counts, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(counts);
        string? before = null;
        if (version is not null)
        {
            (var current, before) = await _read.AtAsync(day, version, token => ConditionalFile.ReadAsync(FullPath(day), token), cancellationToken);
            if (!current)
            {
                return false;
            }
        }

        return await ConditionalFile.WriteAsync(FullPath(day), NoticeCountDocuments.Write(counts, before), version, cancellationToken);
    }

    public async Task ForgetBeforeAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return;
        }

        foreach (var file in System.IO.Directory.EnumerateFiles(Directory).Where(file => NoticeCountDocuments.DayOf(file) < day).ToArray())
        {
            await ConditionalFile.DeleteAsync(file, cancellationToken);
        }
    }

    private string FullPath(DateOnly day) => Path.Combine(Directory, NoticeCountDocuments.NameOf(day));
}
