using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// How a table's match was created and where its decisions are kept: with the journal, all it takes to build
/// the match again as it stands (ADR 0159). The engine draws every roll from the seed, so the same decisions
/// in the same order give the same board.
/// </summary>
/// <param name="Run">The name the table's journal is kept under.</param>
/// <param name="MatchId">The id the match is created under, every time it is built.</param>
/// <param name="Roster">The creatures each player brings.</param>
public sealed record JournaledMatch(string Run, MatchId MatchId, RuleSet Rules, int Seed, IReadOnlyList<CreatureDefinitionId> Roster)
{
    /// <summary>
    /// Whether the journal is created empty with the run, before the match is: a recorded table's (#874). Such a journal
    /// read absent was deleted with its run, from the first read on, and is never created again by a step.
    /// </summary>
    public bool Recorded { get; init; }
}
