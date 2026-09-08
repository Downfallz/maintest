namespace DA.Game.Application.Matches.DTOs;

using DA.Game.Shared.Contracts.Matches.Enums;

public sealed record MatchLifecycleDto(
    MatchState State,
    bool IsStarted,
    bool IsWaitingForPlayers,
    bool HasEnded
);
