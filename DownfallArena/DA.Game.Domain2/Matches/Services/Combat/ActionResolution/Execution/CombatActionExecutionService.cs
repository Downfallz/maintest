using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;

public sealed class CombatActionExecutionService(IInstantEffectService instantEffectService) : ICombatActionExecutionService
{
    // -------------------------------
    // INVARIANT FAILURES (Ixxx)
    // -------------------------------
    private const string INV_I201_TARGET_NOT_FOUND =
        "I201 - Target creature was not found in match when applying combat result.";

    public Result ApplyCombatResult(CombatActionResult result, IReadOnlyList<CombatCreature> allCreatures)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(allCreatures);

        // 1) Apply instant effects
        foreach (var instant in result.InstantEffects)
        {
            var targetResult = FindCreature(instant.TargetId, allCreatures);
            if (!targetResult.IsSuccess)
                return Result.InvariantFail(targetResult.Error!);

            instantEffectService.ApplyInstantEffect(instant, targetResult.Value!);
        }

        return Result.Ok();
    }

    private static Result<CombatCreature> FindCreature(
        CreatureId id,
        IReadOnlyList<CombatCreature> allCreatures)
    {
        var creature = allCreatures.SingleOrDefault(x => x.Id == id);
        if (creature is null)
            return Result<CombatCreature>.InvariantFail(INV_I201_TARGET_NOT_FOUND);

        return Result<CombatCreature>.Ok(creature);
    }
}
