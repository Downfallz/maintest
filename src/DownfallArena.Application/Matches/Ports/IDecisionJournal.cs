using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;

namespace DownfallArena.Application.Matches.Ports;

/// <summary>
/// Every decision a table's match took, in order, kept under the name of the table's run. With how the match
/// was created these are the match (ADR 0159): it lives in memory only as long as somebody needs it, and is
/// rebuilt from here. Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IDecisionJournal
{
    /// <summary>The decisions written so far and where the journal ends; nothing, at the start, for a run nobody wrote.</summary>
    Task<JournalReading> ReadAsync(string run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes decisions after the others, if the journal still ends where the caller read it to. Null, and
    /// nothing written, when somebody else wrote first: two writers cannot both write the same line.
    /// </summary>
    Task<JournalPosition?> AppendAsync(string run, JournalPosition after, IReadOnlyList<SeatDecision> decisions, CancellationToken cancellationToken = default);
}
