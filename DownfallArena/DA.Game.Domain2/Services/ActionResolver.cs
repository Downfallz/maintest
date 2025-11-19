using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.RuleSets;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Services;
public sealed class ActionResolver
{
    public CombatActionResult Resolve(CombatActionChoice intent, RuleSet rules, IClock clock)
    {
        // orchestrer le pipeline (cost, hit, crit, damage, status, death check, aftermath)
        throw new NotImplementedException();
    }
}
