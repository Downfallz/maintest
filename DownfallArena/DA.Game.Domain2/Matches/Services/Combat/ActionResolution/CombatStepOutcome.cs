using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Aggregates
{
public sealed partial class Match
    {
        // --------------------
        // Combat – resolution
        // --------------------

        public sealed record CombatStepOutcome(
    bool DidResolveStep,
    bool IsRoundCompleted,
    bool IsMatchEnded,
    MatchState MatchState,
    MatchId MatchId,
    RoundId? RoundId,
    int RoundNumber);
    }
}
