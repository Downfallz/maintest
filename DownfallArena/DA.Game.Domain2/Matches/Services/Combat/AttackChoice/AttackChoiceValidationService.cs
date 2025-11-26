using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Matches.Services.Combat.Resolution;

public sealed class AttackChoiceValidationService(
    IAttackChoicePolicy attackChoicePolicy,
    ICostPolicy costPolicy,
    ITargetingPolicy targetingPolicy)
    : IAttackChoiceValidationService
{
    public Result EnsureSubmittedActionIsValid(GameContext ctx, CombatActionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        // 1) High-level legality (phase, actor status, etc.)
        var actionPolicyResult = attackChoicePolicy.EnsureActionIsValid(ctx);
        if (!actionPolicyResult.IsSuccess)
            return Result.Fail(actionPolicyResult.Error!);

        // 2) Cost / energy
        var costPolicyResult = costPolicy.EnsureCreatureHasEnoughEnergy(ctx, choice);
        if (!costPolicyResult.IsSuccess)
            return Result.Fail(costPolicyResult.Error!);

        // 3) Targeting
        var targetingPolicyResult = targetingPolicy.EnsureCombatActionHasValidTargets(ctx, choice);
        if (!targetingPolicyResult.IsSuccess)
            return Result.Fail(targetingPolicyResult.Error!);

      

        return Result.Ok();
    }
}