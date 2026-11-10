using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Infrastructure.Matches;

/// <summary>
/// Journals as <c>decisions.jsonl</c> files, one in each run's directory under a root: the same file, in the
/// same place, the table has written since ADR 0091. An append is conditional on the file's length.
/// </summary>
/// <remarks>
/// One reader or writer at a time in this process, which is who a directory on this machine serves; a file
/// opened to append is opened for this writer alone, so another process appending at the same moment is
/// refused by the system rather than interleaved.
/// </remarks>
public sealed class FileDecisionJournal : IDecisionJournal
{
    private readonly FileArtifactStore _runs;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();

    public FileDecisionJournal(string rootDirectory, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _runs = new FileArtifactStore(rootDirectory);
        _clock = clock;
    }

    public Task<JournalReading> ReadAsync(string run, CancellationToken cancellationToken = default)
    {
        var path = PathOf(run);
        lock (_gate)
        {
            return Task.FromResult(File.Exists(path) ? JournalLines.Read(File.ReadAllBytes(path)) : JournalReading.Empty);
        }
    }

    public Task<JournalPosition?> AppendAsync(string run, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(entries);
        var path = PathOf(run);
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (stream.Length != after.Mark)
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

    private string PathOf(string run) => Path.Combine(_runs.LocationOf(run), JournalLines.FileName);
}
