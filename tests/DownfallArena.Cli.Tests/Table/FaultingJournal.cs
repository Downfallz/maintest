using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Infrastructure.Matches;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A practice's journal that takes the match's entries before the cut, then refuses every write as a bug would: not
/// a store that is out for a moment, so the session cannot be played on (#931).
/// </summary>
internal sealed class FaultingJournal : IDecisionJournal
{
    private readonly InMemoryDecisionJournal _inner = new();
    private int _writes;

    public Task<JournalReading> ReadAsync(string run, CancellationToken cancellationToken = default) => _inner.ReadAsync(run, cancellationToken);

    public Task<JournalReading?> ReadAfterAsync(string run, JournalPosition after, CancellationToken cancellationToken = default) =>
        _inner.ReadAfterAsync(run, after, cancellationToken);

    public Task<JournalPosition?> AppendAsync(string run, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default) =>
        Interlocked.Increment(ref _writes) == 1 ? _inner.AppendAsync(run, after, entries, cancellationToken) : Refused();

    public Task<JournalPosition?> AppendExistingAsync(string run, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default) => Refused();

    public Task<long> EndAsync(string run, CancellationToken cancellationToken = default) => _inner.EndAsync(run, cancellationToken);

    public Task<JournalWrite?> LastWrittenAsync(string run, CancellationToken cancellationToken = default) => _inner.LastWrittenAsync(run, cancellationToken);

    private static Task<JournalPosition?> Refused() => Task.FromException<JournalPosition?>(new InvalidOperationException("The journal cannot be played on."));
}
