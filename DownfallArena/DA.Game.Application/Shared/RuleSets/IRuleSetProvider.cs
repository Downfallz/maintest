using DA.Game.Domain2.Shared.RuleSets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Shared.RuleSets;

public interface IRuleSetProvider
{
    RuleSet Current { get; }
}
