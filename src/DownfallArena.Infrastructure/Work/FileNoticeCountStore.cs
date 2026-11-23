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
    public FileNoticeCountStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = Path.GetFullPath(directory);
    }

    public string Directory { get; }

    public async Task<NoticeCountRead> ReadAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var (text, version) = await ConditionalFile.ReadAsync(FullPath(day), cancellationToken);
        return new NoticeCountRead(text is null ? [] : NoticeCountDocuments.Read(text), version);
    }

    /// <summary>
    /// Reads the day again to write back what this revision does not know of it (#1151): a write over a version that
    /// has moved since is refused there, without trying it.
    /// </summary>
    public async Task<bool> WriteAsync(DateOnly day, IReadOnlyList<NoticeCount> counts, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(counts);
        string? before = null;
        if (version is not null)
        {
            (before, var current) = await ConditionalFile.ReadAsync(FullPath(day), cancellationToken);
            if (!string.Equals(current, version, StringComparison.Ordinal))
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
