using DownfallArena.Application.Matches.Journaling;

namespace DownfallArena.Application.Matches.Ports;

/// <summary>
/// Everything that happened to a table's match, in order, kept under the name of the table's run: its decisions,
/// a concession, a seat changing hands. With how the match was created these are the match (ADR 0159): it
/// lives in memory only as long as somebody needs it, and is rebuilt from here. Owned by Application,
/// implemented by Infrastructure.
/// </summary>
public interface IDecisionJournal
{
    /// <summary>The entries written so far and where the journal ends; nothing, at the start, for a run nobody wrote.</summary>
    Task<JournalReading> ReadAsync(string run, CancellationToken cancellationToken = default);

    /// <summary>
    /// The entries written after a position read or written earlier, and where the journal ends now: what a
    /// process holding the match as it stood there reads to catch up, rather than the whole journal (inventory
    /// question 13). No entries, at the same position, when nobody wrote since. Nothing when the journal does
    /// not reach the position, so it is not the journal the position was taken from, and is read whole.
    /// </summary>
    Task<JournalReading?> ReadAfterAsync(string run, JournalPosition after, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes entries after the others, if the journal still ends where the caller read it to. Null, and
    /// nothing written, when somebody else wrote first: two writers cannot both write the same line.
    /// </summary>
    Task<JournalPosition?> AppendAsync(string run, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Where the journal ends, as the store measures it (<see cref="JournalPosition.Mark" />), read without reading
    /// the journal: what a process holding the table watches to learn of a line another process wrote (ADR 0162).
    /// Zero for a run nobody wrote.
    /// </summary>
    Task<long> EndAsync(string run, CancellationToken cancellationToken = default);

    /// <summary>
    /// When the journal was last written, as the store keeps it, read without reading the journal: how long a table
    /// has waited for its next move (ADR 0169). None for a run nobody wrote.
    /// </summary>
    Task<DateTimeOffset?> LastWrittenAsync(string run, CancellationToken cancellationToken = default);
}
