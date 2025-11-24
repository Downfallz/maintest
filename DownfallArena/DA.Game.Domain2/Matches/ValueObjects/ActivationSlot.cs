using DA.Game.Domain2.Matches.Entities;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources.Stats;
namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record ActivationSlot(
    PlayerSlot PlayerSlot,
    CombatCreature CombatCharacter,
    Speed Speed,
    Initiative InitiativeValue // valeur figée pour l’ordre d’activation
);