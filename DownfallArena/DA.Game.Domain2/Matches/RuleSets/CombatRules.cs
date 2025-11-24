using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Policies.Evolution;
using DA.Game.Domain2.Matches.Services.Combat;
using DA.Game.Domain2.Matches.Services.Combat.Execution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.RuleSets;

public class CombatRules(ICombatActionResolutionService combatActionResolutionService,
    IEffectExecutionService effectExecutionService)
{
    public Result<CombatActionResult> Resolve(CombatActionChoice intent, Match match)
    {
        return combatActionResolutionService.Resolve(intent, match);
    }

    public Result ApplyCombatResult(CombatActionResult actionResult, Match match)
    {
        return effectExecutionService.ApplyCombatResult(actionResult, match);
    }
}
