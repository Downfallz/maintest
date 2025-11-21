namespace DA.Game.Application.Matches.DTOs;

using DA.Game.Shared.Contracts.Matches.Enums;

public sealed record ActivationSlotDto(
    PlayerSlot PlayerSlot,
    CombatCharacterDto CombatCharacter,
    Speed Speed,
    int InitiativeValue
);
