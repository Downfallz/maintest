using DA.Game.Domain2.Catalog.Ids;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Shared.Ids;

namespace DA.Game.Domain2.Matches.Resources;
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
