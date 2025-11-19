using DA.Game.Shared;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;      // MatchId

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
