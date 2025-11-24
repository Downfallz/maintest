using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Shared.Contracts.Resources.Creatures;

public sealed record CreatureDefinitionRef(
    CreatureDefId Id,
    string Name,
    Health BaseHp,
    Energy BaseEnergy,
    Defense BaseDefense,
    Initiative BaseInitiative,
    CriticalChance BaseCritChance,
    IReadOnlyList<SpellId> StartingSpellIds
)
{
    public static CreatureDefinitionRef Create(
        CreatureDefId id,
        string name,
        Health baseHp,
        Energy baseEnergy,
        Defense baseDefense,
        Initiative baseInitiative,
        CriticalChance baseCritChance,
        IReadOnlyList<SpellId> startingSpellIds)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        if (startingSpellIds is null || startingSpellIds.Count == 0)
            throw new ArgumentException("StartingSpellIds cannot be null or empty.", nameof(startingSpellIds));

        // Optionnel mais safe : éviter les SpellId par défaut
        for (var i = 0; i < startingSpellIds.Count; i++)
        {
            if (startingSpellIds[i].Equals(default(SpellId)))
                throw new ArgumentException("StartingSpellIds cannot contain default SpellId values.", nameof(startingSpellIds));
        }

        return new CreatureDefinitionRef(
            id,
            name,
            baseHp,
            baseEnergy,
            baseDefense,
            baseInitiative,
            baseCritChance,
            startingSpellIds
        );
    }
}
