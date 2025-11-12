using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using DA.Game.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Services;

public interface IEffectStep
{
    CombatActionResult Execute(CombatActionResult acc, CombatActionIntent intent, RuleSet rules, IClock clock);
}
