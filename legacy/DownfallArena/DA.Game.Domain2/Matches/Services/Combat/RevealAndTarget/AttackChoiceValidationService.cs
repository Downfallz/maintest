using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;
using System;

namespace DA.Game.Domain2.Matches.Services.Combat.Resolution;

public sealed class AttackChoiceValidationService(
    ICombatActionSelectionPolicy attackChoicePolicy,
    ICostPolicy costPolicy,
    ITargetingPolicy targetingPolicy)
    : IAttackChoiceValidationService
{
    public Result EnsureSubmittedActionIsValid(CreaturePerspective ctx, CombatActionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(choice);

        // 1) High-level legality (phase, actor status, etc.)
        var actionPolicyResult = attackChoicePolicy.EnsureActionIsValid(ctx);
        if (!actionPolicyResult.IsSuccess)
        {
            // Preserve invariant flag and error message
            return actionPolicyResult;
        }

        // 2) Cost / energy
        var costPolicyResult = costPolicy.EnsureCreatureHasEnoughEnergy(ctx, choice.SpellRef);
        if (!costPolicyResult.IsSuccess)
        {
            // Preserve invariant flag and error message
            return costPolicyResult;
        }

        // 3) Targeting
        var targetingPolicyResult = targetingPolicy.EnsureCombatActionHasValidTargets(ctx, choice);

        // 3.a) Hard failure (invariant / impossible state)
        if (!targetingPolicyResult.IsSuccess)
        {
            return targetingPolicyResult.IsInvariant
                ? Result.InvariantFail(targetingPolicyResult.Error!)
                : Result.Fail(targetingPolicyResult.Error!);
        }

        var report = targetingPolicyResult.Value!;

        // 3.b) Any domain targeting error should prevent submitting the action
        if (report.Failures.Count > 0)
        {
            // At this stage we do not allow partial acceptance: any failure blocks the choice.
            var first = report.Failures[0];

            // You can choose Message instead of ErrorCode depending on UI needs
            return Result.Fail(first.ErrorCode);
        }

        // Everything is valid for submission
        return Result.Ok();
    }
}
