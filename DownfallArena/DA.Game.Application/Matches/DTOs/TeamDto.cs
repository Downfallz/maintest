namespace DA.Game.Application.Matches.DTOs;

public sealed record TeamDto(
    IReadOnlyList<CombatCharacterDto> Characters
);