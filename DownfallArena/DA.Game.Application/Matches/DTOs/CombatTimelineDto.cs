namespace DA.Game.Application.Matches.DTOs;

public sealed record CombatTimelineDto(
    IReadOnlyList<ActivationSlotDto> Slots
);
