using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.Services.Combat.ActionResolution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Services.Combat.Resolution;

public sealed class CombatActionResolutionService(
    ICombatActionResolutionPolicy combatActionPolicy,
    IUnlockedPolicy unlockedPolicy,
    ICostPolicy costPolicy,
    ITargetingPolicy targetingPolicy,
    IEffectComputationService effectComputationService,
    ICritComputationService critComputationService)
    : ICombatActionResolutionService
{
    public Result<CombatActionResult> Resolve(CreaturePerspective ctx, CombatActionChoice choice)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(choice);

        // 1) High-level legality: if this fails, we stop here (full fizzle).
        var actionPolicyResult = combatActionPolicy.EnsureActionIsValid(ctx);
        if (!actionPolicyResult.IsSuccess)
            return actionPolicyResult.To<CombatActionResult>();

        // 2) High-level legality: if this fails, we stop here (full fizzle).
        var unlockResult = unlockedPolicy.EnsureCreatureHasUnlockedSpell(ctx, choice.SpellRef);
        if (!unlockResult.IsSuccess)
            return unlockResult.To<CombatActionResult>();

        // 3) Cost / energy: if this fails, we stop here (full fizzle).
        var costPolicyResult = costPolicy.EnsureCreatureHasEnoughEnergy(ctx, choice.SpellRef);
        if (!costPolicyResult.IsSuccess)
            return costPolicyResult.To<CombatActionResult>();

        // 4) Targeting: Result<T> is Ok, report can contain failures.
        var targetingPolicyResult = targetingPolicy.EnsureCombatActionHasValidTargets(ctx, choice);

        if (!targetingPolicyResult.IsSuccess)
        {
            // Only used for invariant / impossible states.
            return targetingPolicyResult.IsInvariant
                ? Result<CombatActionResult>.InvariantFail(targetingPolicyResult.Error!)
                : Result<CombatActionResult>.Fail(targetingPolicyResult.Error!);
        }

        var report = targetingPolicyResult.Value!;

        // 4.a) Global failures => full fizzle (domain-flow error).
        if (report.GlobalFailures.Any())
        {
            var first = report.GlobalFailures.First();
            return Result<CombatActionResult>.Fail(first.ErrorCode);
        }

        // 4.b) Per-target failures => partial fizzle possible.
        var invalidTargetIds = new HashSet<CreatureId>(report.InvalidTargetIds.Select(x => x!.Value!));

        var originalTargets = choice.TargetIds ?? Array.Empty<CreatureId>();
        var allowedTargets = originalTargets
            .Where(id => !invalidTargetIds.Contains(id))
            .ToArray();

        // If no valid targets remain => full fizzle (domain-flow error).
        if (allowedTargets.Length == 0)
        {
            var firstFailure = report.PerTargetFailures.FirstOrDefault();
            var errorMessage = firstFailure?.Message?? "D4XX_ALL_TARGETS_INVALID";

            return Result<CombatActionResult>.Fail(errorMessage);
        }

        // 5) Compute raw effects based on the effective targets.
        var effectiveChoice = choice.WithFilteredTargets(allowedTargets);
        var raw = effectComputationService.ComputeRawEffects(effectiveChoice);

        // 6) Compute crit based on the effective choice.
        var crit = critComputationService.ApplyCrit(ctx, effectiveChoice);

        // 7S) Domain result: "what should happen", no mutation here.
        var result = new CombatActionResult(
            originalChoice: choice,
            effectiveChoice: effectiveChoice,
            instantEffects: raw.InstantEffects,
            overtimeEffects: raw.Conditions,
            critical: crit,
            targetingFailures: report.PerTargetFailures);

        return Result<CombatActionResult>.Ok(result);
    }
}
