using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.Execution;

public class EffectExecutionService(IInstantEffectService instantEffectService) : IEffectExecutionService
{

    public Result ApplyCombatResult(CombatActionResult result, Match match)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(match);

        // 1) Apply instant effects
        foreach (var instant in result.InstantEffects)
        {
            var resulttarget = FindCreature(instant.TargetId, match);
            if (!resulttarget.IsSuccess)
                return Result.Fail(resulttarget.Error!);

            instantEffectService.ApplyInstantEffect(match, resulttarget.Value!, instant);
        }
        return Result.Ok();
        // 2) Apply new conditions (DoT, buffs, debuffs, permanent)
        //foreach (var cond in result.NewConditions)
        //{
        //    var target = FindCreature(cond.TargetId);
        //    if (target is null)
        //        continue;

        //    target.AddCondition(cond.Condition);
        //}
    }

    private static Result<CombatCreature> FindCreature(CreatureId id, Match match)
    {
        var creature = match.AllCreatures.SingleOrDefault(x => x.Id == id);
        if (creature is null)
            return Result<CombatCreature>.Fail("Could not find creature in match");

        return Result<CombatCreature>.Ok(creature);
    }
}
