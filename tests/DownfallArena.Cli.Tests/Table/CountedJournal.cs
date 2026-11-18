using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>A journal that counts how often its last write is asked for: what a listing pays per row (#775).</summary>
internal sealed class CountedJournal(IDecisionJournal inner) : IDecisionJournal
{
    private int _lastWrittenReads;

    /// <summary>How many times <see cref="LastWrittenAsync" /> was asked, every run together.</summary>
    public int LastWrittenReads => Volatile.Read(ref _lastWrittenReads);

    /// <summary>Whether asking for the last write fails, as a store that is out does.</summary>
    public bool LastWrittenFails { get; set; }

    public Task<JournalReading> ReadAsync(string run, CancellationToken cancellationToken = default) => inner.ReadAsync(run, cancellationToken);

    public Task<JournalReading?> ReadAfterAsync(string run, JournalPosition after, CancellationToken cancellationToken = default) =>
        inner.ReadAfterAsync(run, after, cancellationToken);

    public Task<JournalPosition?> AppendAsync(string run, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default) =>
        inner.AppendAsync(run, after, entries, cancellationToken);

    public Task<JournalPosition?> AppendExistingAsync(string run, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default) =>
        inner.AppendExistingAsync(run, after, entries, cancellationToken);

    public Task<long> EndAsync(string run, CancellationToken cancellationToken = default) => inner.EndAsync(run, cancellationToken);

    public Task<JournalWrite?> LastWrittenAsync(string run, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _lastWrittenReads);
        return LastWrittenFails ? Task.FromException<JournalWrite?>(new IOException("The journal is out.")) : inner.LastWrittenAsync(run, cancellationToken);
    }
}
