using DownfallArena.SharedKernel.Identifiers;

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
        CreatureStats baseStats,
        TalentTreeId talentTree,
        IReadOnlyList<SpellId> startingSpells)
    {
        Id = id;
        Name = name;
        CreatureClass = creatureClass;
        BaseStats = baseStats;
        TalentTree = talentTree;
        StartingSpells = startingSpells;
    }

    public CreatureDefinitionId Id { get; }

    public string Name { get; }

    public CreatureClass CreatureClass { get; }

    public CreatureStats BaseStats { get; }

    public TalentTreeId TalentTree { get; }

    public IReadOnlyList<SpellId> StartingSpells { get; }

    public static CreatureDefinition Create(
        CreatureDefinitionId id,
        string name,
        CreatureClass creatureClass,
        CreatureStats baseStats,
        TalentTreeId talentTree,
        IReadOnlyList<SpellId> startingSpells)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(baseStats);
        ArgumentNullException.ThrowIfNull(talentTree);
        ArgumentNullException.ThrowIfNull(startingSpells);

        if (startingSpells.Count == 0)
        {
            throw new ArgumentException("A creature definition must have at least one starting spell.", nameof(startingSpells));
        }

        return new CreatureDefinition(id, name, creatureClass, baseStats, talentTree, [.. startingSpells]);
    }
}
