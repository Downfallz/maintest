using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Players.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Simulation.Matches.Results;
public sealed record MatchResult(
    MatchId MatchId,
    string Scenario,
    int TurnsPlayed,
    MatchState FinalState,
    PlayerSlot? LastTurnBy,
    PlayerId? Winner // null si pas de condition de victoire encore
);