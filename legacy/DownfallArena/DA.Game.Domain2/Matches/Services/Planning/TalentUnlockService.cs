using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Talents;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Planning;

public sealed class TalentUnlockService : ITalentUnlockService
{
    public Result<IReadOnlyList<SpellId>> GetUnlockableSpells(
        CreatureSnapshot creature,
        TalentTree? tree)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(tree);

        // If creature is dead, or has no tree → nothing to unlock
        if (creature.IsDead)
            return Result<IReadOnlyList<SpellId>>.Ok( Array.Empty<SpellId>());

        if (creature.TalentTreeId is null ||
            creature.TalentTreeId != tree.Id)
        {
            // Different tree than the one assigned to the creature
            return Result<IReadOnlyList<SpellId>>.Ok(Array.Empty<SpellId>());
        }

        var known = creature.KnownSpellIds;
        var unlockable = new List<SpellId>();

        CollectUnlockable(tree.Root, known, unlockable);

        return Result<IReadOnlyList<SpellId>>.Ok(unlockable);
    }
    public Result ValidateSpellUnlock(
        CreaturePerspective creaturePerspective,
        TalentTree? tree,
        SpellUnlockChoice choice)
    {
        ArgumentNullException.ThrowIfNull(creaturePerspective);
        ArgumentNullException.ThrowIfNull(choice);

        var creature = creaturePerspective.Actor;

        if (creature.IsDead)
            return Result.Fail("D7XX_CREATURE_DEAD");

        if ((creature.TalentTreeId is null && tree is not null) ||
            creature.TalentTreeId != tree?.Id)
            return Result.Fail("D7XY_INVALID_TALENT_TREE_FOR_CREATURE");

        if (creature.KnowsSpell(choice.SpellRef.Id))
            return Result.Fail("D7XZ_ALREADY_KNOWS_SPELL");

        var unlockable = GetUnlockableSpells(creature, tree);
        if (!unlockable.Value!.Contains(choice.SpellRef.Id))
            return Result.Fail("D7XU_SPELL_NOT_UNLOCKABLE");

        return Result.Ok();
    }


    private static void CollectUnlockable(
        TalentTreeNode node,
        IReadOnlyList<SpellId> known,
        List<SpellId> buffer)
    {
        // If node-level prerequisites are not satisfied, we do not
        // expose any of its spells, nor recurse into children.
        if (!ArePrerequisitesSatisfied(node.Prerequisites, known))
            return;

        // Node is "open" → its spells may be unlockable if:
        //  - not already known
        //  - their own prerequisites are satisfied
        foreach (var spellNode in node.Spells)
        {
            var spellId = spellNode.SpellId;

            if (known.Contains(spellId))
                continue;

            if (!ArePrerequisitesSatisfied(spellNode.Prerequisites, known))
                continue;

            buffer.Add(spellId);
        }

        // Recurse into children only if this node is open
        foreach (var child in node.Children)
        {
            CollectUnlockable(child, known, buffer);
        }
    }

    private static bool ArePrerequisitesSatisfied(
        TalentPrerequisites prereq,
        IReadOnlyList<SpellId> known)
    {
        // allOf: every required spell must be known
        if (prereq.AllOf is { Count: > 0 })
        {
            for (var i = 0; i < prereq.AllOf.Count; i++)
            {
                if (!known.Contains(prereq.AllOf[i]))
                    return false;
            }
        }

        // anyOf: at least one of the spells must be known
        if (prereq.AnyOf is { Count: > 0 })
        {
            var hasAny = false;
            for (var i = 0; i < prereq.AnyOf.Count; i++)
            {
                if (known.Contains(prereq.AnyOf[i]))
                {
                    hasAny = true;
                    break;
                }
            }

            if (!hasAny)
                return false;
        }

        return true;
    }
}
