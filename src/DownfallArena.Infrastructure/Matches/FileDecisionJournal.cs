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
/// while a table is being played. Between processes, Linux takes a lock on the file for the append, and a
/// writer that finds it taken is told what one that lost the race is told, and reads again; Windows refuses
/// a second writer where the file is opened, which that writer gets as the error it is.
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

    public Task<JournalPosition?> AppendAsync(string run, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(entries);
        var path = PathOf(run);
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            if (!Alone(stream) || stream.Length != after.Mark)
            {
                return Task.FromResult<JournalPosition?>(null);
            }

            if (entries.Count == 0)
            {
                return Task.FromResult<JournalPosition?>(after);
            }

            stream.Seek(0, SeekOrigin.End);
            stream.Write(JournalLines.Write(after, entries, _clock.GetUtcNow()));
            stream.Flush(flushToDisk: true);
            return Task.FromResult<JournalPosition?>(new JournalPosition(after.Lines + entries.Count, stream.Length));
        }
    }

    /// <summary>
    /// Whether no other process is appending to the file. Asked where opening the file does not already say:
    /// Windows refuses a second writer at the open, and nothing is asked where the system has no such lock.
    /// </summary>
    private static bool Alone(FileStream stream)
    {
        if (!OperatingSystem.IsLinux())
        {
            return true;
        }

        try
        {
            // Held until the stream is closed, which is when the line is written.
            stream.Lock(0, long.MaxValue);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private string PathOf(string run) => Path.Combine(_runs.LocationOf(run), JournalLines.FileName);
}
