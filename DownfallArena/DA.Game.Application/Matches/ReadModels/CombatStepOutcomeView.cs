using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.ReadModels;

public sealed record CombatStepOutcomeView(
    bool DidResolveStep,
    bool IsRoundCompleted,
    bool IsMatchEnded,
    MatchState MatchState,
    MatchId MatchId,
    RoundId? RoundId,
    int RoundNumber);
