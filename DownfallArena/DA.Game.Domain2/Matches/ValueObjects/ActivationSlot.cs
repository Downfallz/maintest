using DA.Game.Domain2.Matches.Entities;
using DA.Game.Shared.Contracts.Matches.Enums;
namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record ActivationSlot(
    PlayerSlot PlayerSlot,
    CombatCharacter CombatCharacter,
    Speed Speed,
    int InitiativeValue // valeur figée pour l’ordre d’activation
);