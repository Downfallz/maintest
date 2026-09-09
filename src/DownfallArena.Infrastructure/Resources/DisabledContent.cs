using DownfallArena.Infrastructure.Resources.Schema;

namespace DownfallArena.Infrastructure.Resources;

/// <summary>
/// Applies the authoring-only <c>enabled</c> switch (ADR 0015): items marked <c>"enabled": false</c> leave the
/// consolidated schema, references to a disabled spell are pruned, and a creature whose talent tree is disabled
/// is a problem. Every item that survives comes out with the switch cleared, so the flag never reaches
/// <c>game.schema.json</c> and content where nothing is disabled hashes as it did before the switch existed.
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
            TalentTrees = [.. authored.TalentTrees.Where(tree => tree.Enabled is not false).Select(tree => Prune(tree, disabledSpells, notes))],
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
        IReadOnlySet<string> disabledSpells,
        IReadOnlySet<string> disabledTrees,
        ICollection<string>? notes,
        ICollection<string> problems)
    {
        if (disabledTrees.Contains(creature.TalentTreeId))
        {
            problems.Add($"creature '{creature.Id}': talent tree '{creature.TalentTreeId}' is disabled. Disable the creature too, or point it at another tree.");
        }

        return creature with
        {
            Enabled = null,
            StartingSpellIds = [.. Keep(creature.StartingSpellIds, disabledSpells, notes, $"creature '{creature.Id}' starting spells")],
        };
    }

    private static TalentTreeDto Prune(TalentTreeDto tree, IReadOnlySet<string> disabledSpells, ICollection<string>? notes) =>
        tree with
        {
            Enabled = null,
            Root = tree.Root is null ? null : Prune(tree.Root, disabledSpells, notes, $"talent tree '{tree.Id}'"),
        };

    private static TalentNodeDto Prune(TalentNodeDto node, IReadOnlySet<string> disabledSpells, ICollection<string>? notes, string context)
    {
        var nodeContext = $"{context}, node '{node.Code}'";
        return node with
        {
            Prerequisites = Prune(node.Prerequisites, disabledSpells, notes, nodeContext),
            Spells =
            [
                .. node.Spells
                    .Where(spell => Keeps(spell.Id, disabledSpells, notes, nodeContext))
                    .Select(spell => spell with { Prerequisites = Prune(spell.Prerequisites, disabledSpells, notes, $"{nodeContext}, spell '{spell.Id}'") }),
            ],
            Children = [.. node.Children.Select(child => Prune(child, disabledSpells, notes, context))],
        };
    }

    private static PrerequisitesDto? Prune(PrerequisitesDto? prerequisites, IReadOnlySet<string> disabledSpells, ICollection<string>? notes, string context) =>
        prerequisites is null
            ? null
            : prerequisites with
            {
                AllOf = [.. Keep(prerequisites.AllOf, disabledSpells, notes, $"{context} prerequisites")],
                AnyOf = [.. Keep(prerequisites.AnyOf, disabledSpells, notes, $"{context} prerequisites")],
            };

    private static IEnumerable<string> Keep(IEnumerable<string> spellIds, IReadOnlySet<string> disabledSpells, ICollection<string>? notes, string context) =>
        spellIds.Where(spellId => Keeps(spellId, disabledSpells, notes, context));

    private static bool Keeps(string spellId, IReadOnlySet<string> disabledSpells, ICollection<string>? notes, string context)
    {
        if (!disabledSpells.Contains(spellId))
        {
            return true;
        }

        notes?.Add($"{context}: dropped disabled spell '{spellId}'.");
        return false;
    }
}
