using System.Diagnostics.CodeAnalysis;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Resources;

/// <summary>
/// In-memory <see cref="IGameResources"/> that validates every cross-reference when created.
/// </summary>
public sealed class GameResources : IGameResources
{
    private readonly Dictionary<CreatureDefinitionId, CreatureDefinition> _creatures;
    private readonly Dictionary<SpellId, Spell> _spells;
    private readonly Dictionary<TalentTreeId, TalentTree> _talentTrees;

    private GameResources(
        string version,
        Dictionary<CreatureDefinitionId, CreatureDefinition> creatures,
        Dictionary<SpellId, Spell> spells,
        Dictionary<TalentTreeId, TalentTree> talentTrees)
    {
        Version = version;
        _creatures = creatures;
        _spells = spells;
        _talentTrees = talentTrees;
    }

    public string Version { get; }

    public IReadOnlyCollection<CreatureDefinition> Creatures => _creatures.Values;

    public IReadOnlyCollection<Spell> Spells => _spells.Values;

    public IReadOnlyCollection<TalentTree> TalentTrees => _talentTrees.Values;

    /// <summary>
    /// Builds the catalogue, or throws <see cref="InvalidGameContentException"/> listing every inconsistency.
    /// </summary>
    public static GameResources Create(
        string version,
        IEnumerable<CreatureDefinition> creatures,
        IEnumerable<Spell> spells,
        IEnumerable<TalentTree> talentTrees)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(talentTrees);

        var problems = new List<string>();
        var spellIndex = Index(spells, spell => spell.Id, "spell", problems);
        var treeIndex = Index(talentTrees, tree => tree.Id, "talent tree", problems);
        var creatureIndex = Index(creatures, creature => creature.Id, "creature", problems);

        foreach (var creature in creatureIndex.Values)
        {
            foreach (var spellId in creature.StartingSpells.Where(spellId => !spellIndex.ContainsKey(spellId)))
            {
                problems.Add($"Creature '{creature.Id.Value}' starts with unknown spell '{spellId.Value}'.");
            }

            if (!treeIndex.ContainsKey(creature.TalentTree))
            {
                problems.Add($"Creature '{creature.Id.Value}' uses unknown talent tree '{creature.TalentTree.Value}'.");
            }
        }

        foreach (var tree in treeIndex.Values)
        {
            foreach (var node in tree.Nodes)
            {
                var referenced = node.Spells.Select(spell => spell.Id)
                    .Concat(node.Prerequisites.ReferencedSpells)
                    .Concat(node.Spells.SelectMany(spell => spell.Prerequisites.ReferencedSpells));

                foreach (var spellId in referenced.Distinct().Where(spellId => !spellIndex.ContainsKey(spellId)))
                {
                    problems.Add($"Talent tree '{tree.Id.Value}', node '{node.Code}' references unknown spell '{spellId.Value}'.");
                }
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidGameContentException(problems);
        }

        return new GameResources(version, creatureIndex, spellIndex, treeIndex);
    }

    public CreatureDefinition GetCreature(CreatureDefinitionId id) =>
        TryGetCreature(id, out var creature) ? creature : throw new KeyNotFoundException($"Unknown creature definition '{id.Value}'.");

    public Spell GetSpell(SpellId id) =>
        TryGetSpell(id, out var spell) ? spell : throw new KeyNotFoundException($"Unknown spell '{id.Value}'.");

    public TalentTree GetTalentTree(TalentTreeId id) =>
        TryGetTalentTree(id, out var talentTree) ? talentTree : throw new KeyNotFoundException($"Unknown talent tree '{id.Value}'.");

    public bool TryGetCreature(CreatureDefinitionId id, [NotNullWhen(true)] out CreatureDefinition? creature)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _creatures.TryGetValue(id, out creature);
    }

    public bool TryGetSpell(SpellId id, [NotNullWhen(true)] out Spell? spell)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _spells.TryGetValue(id, out spell);
    }

    public bool TryGetTalentTree(TalentTreeId id, [NotNullWhen(true)] out TalentTree? talentTree)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _talentTrees.TryGetValue(id, out talentTree);
    }

    private static Dictionary<TId, TItem> Index<TId, TItem>(
        IEnumerable<TItem> items,
        Func<TItem, TId> idOf,
        string kind,
        List<string> problems)
        where TId : notnull
    {
        var index = new Dictionary<TId, TItem>();
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (!index.TryAdd(idOf(item), item))
            {
                problems.Add($"Duplicate {kind} id '{idOf(item)}'.");
            }
        }

        return index;
    }
}
