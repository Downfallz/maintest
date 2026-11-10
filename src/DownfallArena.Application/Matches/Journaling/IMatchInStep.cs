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
}
