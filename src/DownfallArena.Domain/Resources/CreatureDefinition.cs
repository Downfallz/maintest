using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Resources;

/// <summary>
/// Static description of a kind of creature: base stats, starting spells, and talent tree.
/// </summary>
public sealed class CreatureDefinition
{
    private CreatureDefinition(
        CreatureDefinitionId id,
        string name,
        CreatureClass creatureClass,
        Health baseHealth,
        Energy baseEnergy,
        Defense baseDefense,
        Initiative baseInitiative,
        CriticalChance baseCriticalChance,
        TalentTreeId talentTree,
        IReadOnlyList<SpellId> startingSpells)
    {
        Id = id;
        Name = name;
        CreatureClass = creatureClass;
        BaseHealth = baseHealth;
        BaseEnergy = baseEnergy;
        BaseDefense = baseDefense;
        BaseInitiative = baseInitiative;
        BaseCriticalChance = baseCriticalChance;
        TalentTree = talentTree;
        StartingSpells = startingSpells;
    }

    public CreatureDefinitionId Id { get; }

    public string Name { get; }

    public CreatureClass CreatureClass { get; }

    public Health BaseHealth { get; }

    public Energy BaseEnergy { get; }

    public Defense BaseDefense { get; }

    public Initiative BaseInitiative { get; }

    public CriticalChance BaseCriticalChance { get; }

    public TalentTreeId TalentTree { get; }

    public IReadOnlyList<SpellId> StartingSpells { get; }

    public static CreatureDefinition Create(
        CreatureDefinitionId id,
        string name,
        CreatureClass creatureClass,
        Health baseHealth,
        Energy baseEnergy,
        Defense baseDefense,
        Initiative baseInitiative,
        CriticalChance baseCriticalChance,
        TalentTreeId talentTree,
        IReadOnlyList<SpellId> startingSpells)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(baseHealth);
        ArgumentNullException.ThrowIfNull(baseEnergy);
        ArgumentNullException.ThrowIfNull(baseDefense);
        ArgumentNullException.ThrowIfNull(baseInitiative);
        ArgumentNullException.ThrowIfNull(baseCriticalChance);
        ArgumentNullException.ThrowIfNull(talentTree);
        ArgumentNullException.ThrowIfNull(startingSpells);

        if (startingSpells.Count == 0)
        {
            throw new ArgumentException("A creature definition must have at least one starting spell.", nameof(startingSpells));
        }

        return new CreatureDefinition(
            id, name, creatureClass, baseHealth, baseEnergy, baseDefense, baseInitiative, baseCriticalChance, talentTree, [.. startingSpells]);
    }
}
