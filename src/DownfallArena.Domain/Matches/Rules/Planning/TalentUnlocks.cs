using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Resources.Talents;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// What a talent tree's gates offer a creature: structural reachability through the tree, which is no longer
/// what may be bought.
/// </summary>
/// <remarks>
/// A pick buys a package and the package's prerequisites decide eligibility (ADR 0056,
/// <see cref="TierEligibility"/>). The per-spell gate this file used to apply is gone with it. What is left is
/// the question the content audit asks — which spells a creature could ever come to know on its own tree —
/// and that reading now understates the catalogue, because multiclassing puts every family within reach. The
/// audit moves to packages in the stage that rewrites the content scorer; until then it reads the tree, and
/// says so.
/// </remarks>
public static class TalentUnlocks
{
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
