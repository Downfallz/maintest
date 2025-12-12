using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;
using System;

namespace DA.Game.Domain2.Matches.Services.Combat.Resolution;

public sealed class IntentValidationService(
    ICombatActionSelectionPolicy attackChoicePolicy,
    ICostPolicy costPolicy,
    IUnlockedPolicy unlockedPolicy)
    : IIntentValidationService
{
    public Result EnsureSubmittedIntentIsValid(CreaturePerspective ctx, CombatActionIntent intent)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(intent);

        // 1) High-level legality (phase, actor status, etc.)
        var actionPolicyResult = attackChoicePolicy.EnsureActionIsValid(ctx);
        if (!actionPolicyResult.IsSuccess)
        {
            // Preserve invariant flag and error message
            return actionPolicyResult;
        }

        // 2) Cost / energy
        var unlockResult = unlockedPolicy.EnsureCreatureHasUnlockedSpell(ctx, intent.SpellRef);
        if (!unlockResult.IsSuccess)
        {
            // Preserve invariant flag and error message
            return unlockResult;
        }

        // 3) Cost / energy
        var costPolicyResult = costPolicy.EnsureCreatureHasEnoughEnergy(ctx, intent.SpellRef);
        if (!costPolicyResult.IsSuccess)
        {
            // Preserve invariant flag and error message
            return costPolicyResult;
        }

        // Everything is valid for submission
        return Result.Ok();
    }
}
