using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;

namespace DA.Game.Application.Matches.DTOs;

public sealed record CombatActionChoiceDto(
    CharacterId ActorId,
    Spell SpellRef,
    IReadOnlyList<CharacterId> TargetIds
);