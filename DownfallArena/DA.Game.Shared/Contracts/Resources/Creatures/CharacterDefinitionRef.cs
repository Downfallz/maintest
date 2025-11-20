using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Shared.Contracts.Resources.Creatures;

public sealed record CharacterDefinitionRef(
    CharacterDefId Id,
    string Name,
    Health BaseHp,
    Energy BaseEnergy,
    Defense BaseDefense,
    Initiative BaseInitiative,
    CriticalChance BaseCritChance,
    IReadOnlyList<SpellId> StartingSpellIds
);
