using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// What is told, inside a match's turn, that the match in memory stands where its journal ends (ADR 0159):
/// loaded or rebuilt, or a step written. Whatever the match raised up to that moment is the journal's; whatever
/// it raises after, until the next time it is told, may be a step that is never written.
/// </summary>
public interface IMatchInStep
{
    void InStep(MatchId matchId, JournalPosition position);

    /// <summary>
    /// Told, before lines of the journal are replayed -- the whole journal rebuilt, or the lines another process
    /// wrote taken on -- every seat change the journal holds through them, in order: who each replayed decision
    /// is named after (inventory question 19), the seat changes further down those same lines included.
    /// </summary>
    void Replaying(MatchId matchId, IReadOnlyList<SeatChangeEntry> seatChanges);
}
