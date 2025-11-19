using DA.Game.Domain2.Match.Enums;      // MatchState
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Matches.Ids;      // MatchId

namespace DA.Game.Application.Matches.DTOs;

public sealed record MatchDto(
    MatchId Id,
    MatchState State,
    MatchLifecycleDto Lifecycle,
    PlayerRef? PlayerRef1,
    PlayerRef? PlayerRef2,
    TeamDto? Player1Team,
    TeamDto? Player2Team,
    RoundDto? CurrentRound,
    int RoundNumber
);
