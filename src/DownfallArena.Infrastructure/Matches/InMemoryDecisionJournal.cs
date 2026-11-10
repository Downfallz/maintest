using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;

namespace DownfallArena.Infrastructure.Matches;

/// <summary>
/// Keeps journals in memory for the lifetime of the process: the journal of a table that is told to record
/// nothing, and of tests. It is conditional like the others, so what is built on it meets the same refusals.
/// </summary>
public sealed class InMemoryDecisionJournal : IDecisionJournal
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<IJournalEntry>> _journals = new(StringComparer.Ordinal);

    public Task<JournalReading> ReadAsync(string run, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(run);
        lock (_gate)
        {
            return Task.FromResult(_journals.TryGetValue(run, out var written)
                ? new JournalReading([.. written], new JournalPosition(written.Count, written.Count))
                : JournalReading.Empty);
        }
    }

    public Task<JournalPosition?> AppendAsync(string run, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(run);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(entries);
        lock (_gate)
        {
            var written = _journals.TryGetValue(run, out var journal) ? journal.Count : 0;
            if (written != after.Mark)
            {
                return Task.FromResult<JournalPosition?>(null);
            }

            if (entries.Count == 0)
            {
                return Task.FromResult<JournalPosition?>(after);
            }

            if (journal is null)
            {
                _journals[run] = journal = [];
            }

            journal.AddRange(entries);
            return Task.FromResult<JournalPosition?>(new JournalPosition(journal.Count, journal.Count));
        }
    }
}
