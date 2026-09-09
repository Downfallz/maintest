using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// Which spells of a talent tree a creature may unlock next: a spell is unlockable when its node's prerequisites
/// and its own prerequisites are met by the spells the creature knows, and it is not known yet.
/// </summary>
public static class TalentUnlocks
{
    public static IReadOnlyList<SpellId> UnlockableSpells(CreatureSnapshot creature, TalentTree tree)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(tree);

        if (creature.IsDead)
        {
            return [];
        }

        return [.. Offered(creature.KnownSpells, tree).Where(spell => !creature.KnowsSpell(spell))];
    }

    /// <summary>
    /// Every spell a creature starting with <paramref name="known"/> could ever come to know on this tree: what
    /// the gates offer that set, then what they offer the larger set, until it stops growing. What a creature
    /// knows only ever grows, so a gate still shut here is shut for the whole match.
    /// </summary>
    public static IReadOnlySet<SpellId> ReachableSpells(IEnumerable<SpellId> known, TalentTree tree)
    {
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(tree);

        var reachable = new HashSet<SpellId>(known);
        bool grew;
        do
        {
            grew = false;
            foreach (var spell in Offered(reachable, tree).ToList())
            {
                grew |= reachable.Add(spell);
            }
        }
        while (grew);

        return reachable;
    }

    /// <summary>The spells this tree offers to a creature knowing exactly these: both gates open, known or not.</summary>
    private static IEnumerable<SpellId> Offered(IReadOnlySet<SpellId> known, TalentTree tree) =>
        tree.Nodes
            .Where(node => node.Prerequisites.AreSatisfiedBy(known))
            .SelectMany(node => node.Spells)
            .Where(spell => spell.Prerequisites.AreSatisfiedBy(known))
            .Select(spell => spell.Id)
            .Distinct();
}
