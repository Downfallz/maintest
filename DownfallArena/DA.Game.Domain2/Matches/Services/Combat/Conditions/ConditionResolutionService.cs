using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Entities.Conditions;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;
using DA.Game.Shared.Contracts.Resources.Stats;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.Conditions;

public sealed class ConditionResolutionServiceV1 : IConditionResolutionService
{
    public Result ResolveStartOfRound(IReadOnlyList<CombatCreature> allCreatures)
    {
        ArgumentNullException.ThrowIfNull(allCreatures);

        foreach (var c in allCreatures)
        {
            if (c.IsDead)
                continue;

            // 1) Reset derived state for the new round
            c.IsStunned = false;
            c.BonusDefense = Defense.Of(0);
            c.BonusCritical = CriticalChance.Of(0);

            // 2) Apply active start-of-round conditions
            foreach (var cond in c.Conditions.Active())
            {
                if (cond.Phase != ConditionPhase.StartOfRound)
                    continue;

                ApplyOne(c, cond);
            }

            // 3) Tick durations and cleanup
            c.Conditions.TickAll();
            c.Conditions.RemoveExpired();
        }

        return Result.Ok();
    }

    private static void ApplyOne(CombatCreature creature, ConditionInstance cond)
    {
        switch (cond.Kind)
        {
            case ConditionKind.DamageOverTime:
                // Convention: Modifier is positive damage-per-tick
                if (cond.Modifier > 0)
                    creature.TakeDamage(cond.Modifier);
                break;

            case ConditionKind.Stunned:
                creature.IsStunned = true;
                break;

            default:
                break;
        }
    }
}
