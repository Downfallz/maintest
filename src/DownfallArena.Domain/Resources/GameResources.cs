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
    private readonly Dictionary<TierId, Tier> _tiers;

    private GameResources(
        string version,
        Dictionary<CreatureDefinitionId, CreatureDefinition> creatures,
        Dictionary<SpellId, Spell> spells,
        Dictionary<TalentTreeId, TalentTree> talentTrees,
        Dictionary<TierId, Tier> tiers)
    {
        Version = version;
        _creatures = creatures;
        _spells = spells;
        _talentTrees = talentTrees;
        _tiers = tiers;
    }

    public string Version { get; }

    public IReadOnlyCollection<CreatureDefinition> Creatures => _creatures.Values;

    public IReadOnlyCollection<Spell> Spells => _spells.Values;

    public IReadOnlyCollection<TalentTree> TalentTrees => _talentTrees.Values;

    public IReadOnlyCollection<Tier> Tiers => _tiers.Values;

    /// <summary>
    /// Builds the catalogue, or throws <see cref="InvalidGameContentException"/> listing every inconsistency.
    /// </summary>
    public static GameResources Create(
        string version,
        IEnumerable<CreatureDefinition> creatures,
        IEnumerable<Spell> spells,
        IEnumerable<TalentTree> talentTrees,
        IEnumerable<Tier>? tiers = null)
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

        var tierIndex = Index(tiers ?? [], tier => tier.Id, "tier", problems);
        ValidateTiers(tierIndex, spellIndex, problems);

        if (problems.Count > 0)
        {
            throw new InvalidGameContentException(problems);
        }

        return new GameResources(version, creatureIndex, spellIndex, treeIndex, tierIndex);
    }

    public CreatureDefinition GetCreature(CreatureDefinitionId id) =>
        TryGetCreature(id, out var creature) ? creature : throw new KeyNotFoundException($"Unknown creature definition '{id.Value}'.");

    public Spell GetSpell(SpellId id) =>
        TryGetSpell(id, out var spell) ? spell : throw new KeyNotFoundException($"Unknown spell '{id.Value}'.");

    public TalentTree GetTalentTree(TalentTreeId id) =>
        TryGetTalentTree(id, out var talentTree) ? talentTree : throw new KeyNotFoundException($"Unknown talent tree '{id.Value}'.");

    public Tier GetTier(TierId id) =>
        TryGetTier(id, out var tier) ? tier : throw new KeyNotFoundException($"Unknown tier '{id.Value}'.");

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

    public bool TryGetTier(TierId id, [NotNullWhen(true)] out Tier? tier)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _tiers.TryGetValue(id, out tier);
    }

    /// <summary>
    /// What a package must satisfy beyond its own construction: the spells and prerequisites it names have to
    /// exist, and the prerequisite graph has to be one a creature can actually climb -- acyclic, and with each
    /// step deeper than the one it requires. A negative bonus is not checked here because
    /// <see cref="Initiative" /> already refuses one, and a second rule saying the same thing is a second rule
    /// to keep in agreement.
    /// </summary>
    private static void ValidateTiers(
        Dictionary<TierId, Tier> tiers,
        Dictionary<SpellId, Spell> spells,
        List<string> problems)
    {
        foreach (var tier in tiers.Values)
        {
            foreach (var spell in tier.Spells.Where(spell => !spells.ContainsKey(spell)))
            {
                problems.Add($"Tier '{tier.Id.Value}' teaches unknown spell '{spell.Value}'.");
            }

            foreach (var required in tier.Prerequisites)
            {
                if (!tiers.TryGetValue(required, out var prerequisite))
                {
                    problems.Add($"Tier '{tier.Id.Value}' requires unknown tier '{required.Value}'.");
                    continue;
                }

                // A prerequisite deeper than or level with what it opens cannot be reached first, which is a
                // cycle that a cycle check phrased in terms of reachability would take longer to find.
                if (prerequisite.Level >= tier.Level)
                {
                    problems.Add(
                        $"Tier '{tier.Id.Value}' at level {tier.Level} requires '{required.Value}' at level {prerequisite.Level}; "
                        + "a prerequisite has to sit above what it opens.");
                }
            }
        }

        // Kept although the level rule above already makes a cycle impossible: a prerequisite chain that must
        // strictly decrease in level cannot close, so this can only ever fire beside that rule and never
        // alone. It stays because the level rule is the part likeliest to be relaxed -- a sibling prerequisite
        // at the same level is a reasonable thing to want -- and this is what would be left guarding the graph.
        foreach (var tier in tiers.Values.Where(tier => Reaches(tier.Id, tier.Id, tiers, [])))
        {
            problems.Add($"Tier '{tier.Id.Value}' requires itself through its prerequisites.");
        }
    }

    /// <summary>
    /// Whether <paramref name="target"/> is reachable from <paramref name="from"/> by prerequisites.
    /// <para>
    /// <paramref name="seen"/> is shared across the whole walk, which is sound because the target is fixed for
    /// one call: "does this tier reach the target" depends on the tier and not on the path taken to it, so a
    /// node that failed once cannot succeed from somewhere else. A direct edge is compared before the pruning,
    /// so an immediate self-requirement is found whatever has been visited.
    /// </para>
    /// </summary>
    private static bool Reaches(TierId from, TierId target, Dictionary<TierId, Tier> tiers, HashSet<TierId> seen)
    {
        if (!tiers.TryGetValue(from, out var tier))
        {
            return false;
        }

        foreach (var required in tier.Prerequisites)
        {
            if (required == target || (seen.Add(required) && Reaches(required, target, tiers, seen)))
            {
                return true;
            }
        }

        return false;
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
