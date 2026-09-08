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

        return
        [
            .. tree.Nodes
                .Where(node => node.Prerequisites.AreSatisfiedBy(creature.KnownSpells))
                .SelectMany(node => node.Spells)
                .Where(spell => !creature.KnowsSpell(spell.Id) && spell.Prerequisites.AreSatisfiedBy(creature.KnownSpells))
                .Select(spell => spell.Id)
                .Distinct(),
        ];
    }
}
