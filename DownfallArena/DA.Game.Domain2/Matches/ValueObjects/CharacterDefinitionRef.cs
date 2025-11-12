using DA.Game.Domain2.Catalog.Ids;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Shared.Ids;

namespace DA.Game.Domain2.Matches.ValueObjects;
// Domain2.Matches.ValueObjects
public sealed record CharacterDefinitionRef(
    CharacterDefId Id,
    string Name,
    double BaseHp,
    double BaseEnergy,
    double BaseDefense,
    double BaseInitiative,
    double BaseCritChance,
    IReadOnlyList<SpellId> StartingSpellIds
);
