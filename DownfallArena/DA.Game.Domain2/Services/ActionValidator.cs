using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using DA.Game.Shared;

namespace DA.Game.Domain2.Services;
public sealed class ActionValidator
{
    private readonly RuleSet _rules;

    public ActionValidator(RuleSet rules)
    {
        _rules = rules;
    }

    public Result<CombatActionIntent> Validate(PlayerActionContext context, CombatActionChoice req)
    {
        // utiliser CanActSpecification, CanTargetSpecification, Energy, Cooldown, etc.
        throw new NotImplementedException();
    }
}
