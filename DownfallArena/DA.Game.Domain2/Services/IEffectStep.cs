using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.RuleSets;
using DA.Game.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Services;

public interface IEffectStep
{
    CombatActionResult Execute(CombatActionResult acc, CombatActionChoice intent, RuleSet rules, IClock clock);
}
