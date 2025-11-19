using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Shared.Policies.RuleSets;

public interface IRuleSetProvider
{
    RuleSet Current { get; }
}
