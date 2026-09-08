using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Services.Combat.Conditions;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Domain2.Matches.ValueObjects.Combat;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.RuleSets;

public class ConditionRules(IConditionResolutionService conditionResolutionService)
{
    public Result ResolveStartOfRound(IReadOnlyList<CombatCreature> allCreatures)
    {
        return conditionResolutionService.ResolveStartOfRound(allCreatures);
    }
}
