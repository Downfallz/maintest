using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Infrastructure.Matches;

/// <summary>
/// Journals as <c>decisions.jsonl</c> files, one in each run's directory under a root: the same file, in the
/// same place, the table has written since ADR 0091. An append is conditional on the file's length.
/// </summary>
/// <remarks>
/// One reader or writer at a time in this process, whichever journal over the directory it comes through:
/// that is who a directory on this machine serves, and it is what makes the length read and the line written
/// one step. The file is never opened for this writer alone, since the store lists and reads the same file
/// while a table is being played. Two processes over one directory are not kept apart here: a line is always
/// added at the end of the file, so neither writes over the other's, but both can take the same length for
/// their own. A table shared between processes is kept in a blob store, whose journal is conditional by the
/// service's own rule.
/// </remarks>
public sealed class FileDecisionJournal : IDecisionJournal
{
    // The process's, not the journal's: two hosts over one directory are two journals over the same files.
    private static readonly Lock Gate = new();

    private readonly FileArtifactStore _runs;
    private readonly TimeProvider _clock;

    public FileDecisionJournal(string rootDirectory, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _runs = new FileArtifactStore(rootDirectory);
        _clock = clock;
    }

    public Task<JournalReading> ReadAsync(string run, CancellationToken cancellationToken = default)
    {
        var path = PathOf(run);
        lock (Gate)
        {
            if (!File.Exists(path))
            {
                return Task.FromResult(JournalReading.Empty);
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            return Task.FromResult(JournalLines.Read(bytes));
        }
    }

    public Task<JournalReading?> ReadAfterAsync(string run, JournalPosition after, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(after);
        var path = PathOf(run);
        lock (Gate)
        {
            if (!File.Exists(path))
            {
                return Task.FromResult<JournalReading?>(after.Mark == 0 ? new JournalReading([], after) : null);
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length < after.Mark)
            {
                return Task.FromResult<JournalReading?>(null);
            }

            stream.Position = after.Mark;
            var bytes = new byte[stream.Length - after.Mark];
            stream.ReadExactly(bytes);
            return Task.FromResult<JournalReading?>(JournalLines.Read(bytes, after));
        }
    }

    public Task<JournalPosition?> AppendAsync(string run, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(entries);
        var path = PathOf(run);
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            if (stream.Length != after.Mark)
            {
                return Task.FromResult<JournalPosition?>(null);
            }

            if (entries.Count == 0)
            {
                return Task.FromResult<JournalPosition?>(after);
            }

            stream.Write(JournalLines.Write(after, entries, _clock.GetUtcNow()));
            stream.Flush(flushToDisk: true);
            return Task.FromResult<JournalPosition?>(new JournalPosition(after.Lines + entries.Count, stream.Length));
        }
    }

    public Task<long> EndAsync(string run, CancellationToken cancellationToken = default)
    {
        var file = new FileInfo(PathOf(run));
        return Task.FromResult(file.Exists ? file.Length : 0);
    }

    private string PathOf(string run) => Path.Combine(_runs.LocationOf(run), JournalLines.FileName);
}
