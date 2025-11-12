using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using DA.Game.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Services;
public sealed class ActionResolver
{
    public CombatActionResult Resolve(CombatActionIntent intent, RuleSet rules, IClock clock)
    {
        // orchestrer le pipeline (cost, hit, crit, damage, status, death check, aftermath)
        throw new NotImplementedException();
    }
}
