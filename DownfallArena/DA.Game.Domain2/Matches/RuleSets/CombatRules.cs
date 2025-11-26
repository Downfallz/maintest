using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.RuleSets;

public class CombatRules(IAttackChoiceValidationService attackChoiceValidationService,
    ICombatActionResolutionService combatActionResolutionService,
    IEffectExecutionService effectExecutionService)
{
    public Result ValidateAction(CreaturePerspective ctx, CombatActionChoice choice)
    {
        return attackChoiceValidationService.EnsureSubmittedActionIsValid(ctx, choice);
    }

    public Result<CombatActionResult> Resolve(CreaturePerspective ctx, CombatActionChoice choice)
    {
        return combatActionResolutionService.Resolve(ctx, choice);
    }

    public Result ApplyCombatResult(CombatActionResult actionResult, IReadOnlyList<CombatCreature> allCreatures)
    {
        return effectExecutionService.ApplyCombatResult(actionResult, allCreatures);
    }
}
