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
/// </summary>
internal static class DisabledContent
{
    public static GameSchema Remove(GameSchema authored, ICollection<string>? notes, ICollection<string> problems)
    {
        ArgumentNullException.ThrowIfNull(authored);
        ArgumentNullException.ThrowIfNull(problems);

        var disabledSpells = authored.Spells.Where(spell => spell.Enabled is false).Select(spell => spell.Id).ToHashSet(StringComparer.Ordinal);
        var disabledTrees = authored.TalentTrees.Where(tree => tree.Enabled is false).Select(tree => tree.Id).ToHashSet(StringComparer.Ordinal);

        Note(notes, disabledSpells, "spell");
        Note(notes, disabledTrees, "talent tree");
        Note(notes, authored.Creatures.Where(creature => creature.Enabled is false).Select(creature => creature.Id), "creature");

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

        var anyOf = Keep(prerequisites.AnyOf, disabledSpells, notes, $"{context} prerequisites");
        if (prerequisites.AnyOf.Count > 0 && anyOf.Count == 0)
        {
            problems.Add($"{context}: every spell of its 'anyOf' is disabled. An empty 'anyOf' is no requirement at all, so pruning it would unlock the node instead of closing it. Disable what it gates, or leave one of those spells enabled.");
        }

        return prerequisites with
        {
            AllOf = Keep(prerequisites.AllOf, disabledSpells, notes, $"{context} prerequisites"),
            AnyOf = anyOf,
        };
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
