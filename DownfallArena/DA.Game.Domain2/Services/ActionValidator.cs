using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using DA.Game.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Services;
public sealed class ActionValidator
{
    private readonly RuleSet _rules;
    private readonly PlayerActionContext _context;

    public ActionValidator(RuleSet rules, PlayerActionContext context)
    {
        _rules = rules;
        _context = context;
    }

    public Result<CombatActionIntent> Validate(CombatActionRequest req)
    {
        // utiliser CanActSpecification, CanTargetSpecification, Energy, Cooldown, etc.
        throw new NotImplementedException();
    }
}
