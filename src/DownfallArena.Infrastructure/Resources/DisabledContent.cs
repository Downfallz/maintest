using DownfallArena.Infrastructure.Resources.Schema;

namespace DownfallArena.Infrastructure.Resources;

/// <summary>
/// Applies the authoring-only <c>enabled</c> switch (ADR 0015): items marked <c>"enabled": false</c> leave the
/// consolidated schema, and references to a disabled spell are pruned. Every item that survives comes out with
/// the switch cleared, so the flag never reaches <c>game.schema.json</c> and content where nothing is disabled
/// hashes as it did before the switch existed.
/// <para>
/// Pruning stops where it would change a rule instead of removing content: a creature left with no starting
/// spell, a creature whose talent tree is disabled, and an <c>anyOf</c> gate whose every spell is disabled are
/// problems. That last one matters because an empty <c>anyOf</c> means "no requirement", so pruning it would
/// silently unlock the node rather than close it.
/// </para>
/// <para>
/// A tier is pruned by neither rule, because both ways of pruning one would change what a player may buy. A
/// package that lost a disabled spell is a package nobody authored, and a package whose prerequisite was
/// disabled is a package with nothing in front of it -- which opens a descendant rather than closing it, the
/// same failure as the empty <c>anyOf</c>. Both are problems, so disabling a spell or a tier that something
/// still depends on has to be done deliberately rather than absorbed.
/// </para>
/// </summary>
internal static class DisabledContent
{
    public static GameSchema Remove(GameSchema authored, ICollection<string>? notes, ICollection<string> problems)
    {
        ArgumentNullException.ThrowIfNull(authored);
        ArgumentNullException.ThrowIfNull(problems);

        var disabledSpells = authored.Spells.Where(spell => spell.Enabled is false).Select(spell => spell.Id).ToHashSet(StringComparer.Ordinal);
        var disabledTrees = authored.TalentTrees.Where(tree => tree.Enabled is false).Select(tree => tree.Id).ToHashSet(StringComparer.Ordinal);
        var authoredTiers = authored.Tiers ?? [];
        var disabledTiers = authoredTiers.Where(tier => tier.Enabled is false).Select(tier => tier.Id).ToHashSet(StringComparer.Ordinal);

        Note(notes, disabledSpells, "spell");
        Note(notes, disabledTrees, "talent tree");
        Note(notes, disabledTiers, "tier");
        Note(notes, authored.Creatures.Where(creature => creature.Enabled is false).Select(creature => creature.Id), "creature");

        foreach (var tier in authoredTiers.Where(tier => tier.Enabled is not false))
        {
            foreach (var spell in tier.Spells.Where(disabledSpells.Contains))
            {
                problems.Add(
                    $"Tier '{tier.Id}' teaches disabled spell '{spell}'. A package is bought whole, so dropping one of "
                    + "its spells would sell something nobody authored: disable the tier too, or re-author it.");
            }

            foreach (var required in tier.Prerequisites.Where(disabledTiers.Contains))
            {
                problems.Add(
                    $"Tier '{tier.Id}' requires disabled tier '{required}'. Dropping the requirement would open the "
                    + "package instead of closing it: disable this tier too, or give it another prerequisite.");
            }
        }

        return authored with
        {
            Creatures =
            [
                .. authored.Creatures
                    .Where(creature => creature.Enabled is not false)
                    .Select(creature => Prune(creature, disabledSpells, disabledTrees, notes, problems)),
            ],
            Spells = [.. authored.Spells.Where(spell => spell.Enabled is not false).Select(spell => spell with { Enabled = null })],
            TalentTrees = [.. authored.TalentTrees.Where(tree => tree.Enabled is not false).Select(tree => Prune(tree, disabledSpells, notes, problems))],
            Tiers = [.. authoredTiers.Where(tier => tier.Enabled is not false).Select(tier => tier with { Enabled = null })],
        };
    }

    private static void Note(ICollection<string>? notes, IEnumerable<string> disabled, string kind)
    {
        if (notes is null)
        {
            return;
        }

        foreach (var id in disabled)
        {
            notes.Add($"Disabled {kind} '{id}' is not in the build.");
        }
    }

    private static CreatureDefinitionDto Prune(
        CreatureDefinitionDto creature,
        HashSet<string> disabledSpells,
        HashSet<string> disabledTrees,
        ICollection<string>? notes,
        ICollection<string> problems)
    {
        var context = $"creature '{creature.Id}'";
        if (disabledTrees.Contains(creature.TalentTreeId))
        {
            problems.Add($"{context}: talent tree '{creature.TalentTreeId}' is disabled. Disable the creature too, or point it at another tree.");
        }

        var startingSpells = Keep(creature.StartingSpellIds, disabledSpells, notes, $"{context} starting spells");
        if (creature.StartingSpellIds.Count > 0 && startingSpells.Count == 0)
        {
            problems.Add($"{context}: every starting spell is disabled, and a creature needs at least one. Disable the creature too, or give it a spell that is enabled.");
        }

        return creature with { Enabled = null, StartingSpellIds = startingSpells };
    }

    private static TalentTreeDto Prune(TalentTreeDto tree, HashSet<string> disabledSpells, ICollection<string>? notes, ICollection<string> problems) =>
        tree with
        {
            Enabled = null,
            Root = tree.Root is null ? null : Prune(tree.Root, disabledSpells, notes, problems, $"talent tree '{tree.Id}'"),
        };

    private static TalentNodeDto Prune(TalentNodeDto node, HashSet<string> disabledSpells, ICollection<string>? notes, ICollection<string> problems, string context)
    {
        var nodeContext = $"{context}, node '{node.Code}'";
        return node with
        {
            Prerequisites = Prune(node.Prerequisites, disabledSpells, notes, problems, nodeContext),
            Spells =
            [
                .. node.Spells
                    .Where(spell => Keeps(spell.Id, disabledSpells, notes, nodeContext))
                    .Select(spell => spell with
                    {
                        Prerequisites = Prune(spell.Prerequisites, disabledSpells, notes, problems, $"{nodeContext}, spell '{spell.Id}'"),
                    }),
            ],
            Children = [.. node.Children.Select(child => Prune(child, disabledSpells, notes, problems, context))],
        };
    }

    private static PrerequisitesDto? Prune(
        PrerequisitesDto? prerequisites,
        HashSet<string> disabledSpells,
        ICollection<string>? notes,
        ICollection<string> problems,
        string context)
    {
        if (prerequisites is null)
        {
            return null;
        }

        return prerequisites with
        {
            AllOf = Gate(prerequisites.AllOf, "allOf", disabledSpells, notes, problems, context),
            AnyOf = Gate(prerequisites.AnyOf, "anyOf", disabledSpells, notes, problems, context),
        };
    }

    /// <summary>
    /// One prerequisite list with its disabled spells pruned. A list that was not empty and comes back empty is
    /// a problem, not a pruning: <c>TalentPrerequisites</c> reads both an empty <c>allOf</c> and an empty
    /// <c>anyOf</c> as "no requirement", so emptying either one would unlock what it gates rather than close it.
    /// </summary>
    private static List<string> Gate(
        IReadOnlyList<string> spellIds,
        string which,
        HashSet<string> disabledSpells,
        ICollection<string>? notes,
        ICollection<string> problems,
        string context)
    {
        var kept = Keep(spellIds, disabledSpells, notes, $"{context} prerequisites");
        if (spellIds.Count > 0 && kept.Count == 0)
        {
            problems.Add($"{context}: every spell of its '{which}' is disabled. An empty '{which}' is no requirement at all, so pruning it would unlock the node instead of closing it. Disable what it gates, or leave one of those spells enabled.");
        }

        return kept;
    }

    private static List<string> Keep(IReadOnlyList<string> spellIds, HashSet<string> disabledSpells, ICollection<string>? notes, string context) =>
        [.. spellIds.Where(spellId => Keeps(spellId, disabledSpells, notes, context))];

    private static bool Keeps(string spellId, HashSet<string> disabledSpells, ICollection<string>? notes, string context)
    {
        if (!disabledSpells.Contains(spellId))
        {
            return true;
        }

        notes?.Add($"{context}: dropped disabled spell '{spellId}'.");
        return false;
    }
}
