using DA.Game.Shared.Contracts.Catalog.Ids;

namespace DA.Game.Shared.Resources.Creatures;
// Domain2.Matches.ValueObjects
public sealed record CharacterDefinitionRef(
    CharacterDefId Id,
    string Name,
    int BaseHp,
    int BaseEnergy,
    int BaseDefense,
    int BaseInitiative,
    double BaseCritChance,
    IReadOnlyList<SpellId> StartingSpellIds
);
