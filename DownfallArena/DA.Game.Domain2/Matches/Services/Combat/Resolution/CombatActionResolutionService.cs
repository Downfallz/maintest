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

public sealed class CombatActionResolutionService(
    ICombatActionPolicy combatActionPolicy,
    ICostPolicy costPolicy,
    ITargetingPolicy targetingPolicy,
    IEffectComputationService effectComputationService,
    ICritComputationService critComputationService)
    : ICombatActionResolutionService
{
    public Result<CombatActionResult> Resolve(GameContext ctx, CombatActionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        // 1) High-level legality (phase, actor status, etc.)
        var actionPolicyResult = combatActionPolicy.EnsureActionIsValid(ctx);
        if (!actionPolicyResult.IsSuccess)
            return Result<CombatActionResult>.Fail(actionPolicyResult.Error!);

        // 2) Cost / energy
        var costPolicyResult = costPolicy.EnsureCreatureHasEnoughEnergy(ctx, choice);
        if (!costPolicyResult.IsSuccess)
            return Result<CombatActionResult>.Fail(costPolicyResult.Error!);

        // 3) Targeting
        var targetingPolicyResult = targetingPolicy.EnsureCombatActionHasValidTargets(ctx, choice);
        if (!targetingPolicyResult.IsSuccess)
            return Result<CombatActionResult>.Fail(targetingPolicyResult.Error!);

        // 4) Compute raw effects from the choice (damage, conditions, etc.)
        var raw = effectComputationService.ComputeRawEffects(choice);

        // 5) Compute crit (based on actor + spell + mode)
        var crit = critComputationService.ApplyCrit(ctx, choice);

        // 6) Build the domain result (no mutation here, juste un "what should happen")
        var result = new CombatActionResult(
            choice,
            raw.InstantEffects,
            crit);

        return Result<CombatActionResult>.Ok(result);
    }
}