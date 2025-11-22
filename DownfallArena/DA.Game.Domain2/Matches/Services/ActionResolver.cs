using DA.Game.Domain2.Matches.RuleSets;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Services;

public sealed class ActionResolver
{
    public CombatActionResult Resolve(CombatActionChoice intent, RuleSet rules, IClock clock)
    {
        // orchestrer le pipeline (cost, hit, crit, damage, status, death check, aftermath)
        throw new NotImplementedException();
    }
}
