using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Application.Simulation.NewFolder;

public sealed record MatchResult(
    MatchId MatchId,
    string Scenario,
    int RoundsPlayed,
    int CombatStepsResolved,
    MatchState FinalState,
    PlayerSlot? Winner,
    MatchEndReason EndReason,
    MatchStats? Stats
);
