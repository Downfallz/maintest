using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.RuleSets;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Services;
public sealed class ActionValidator
{
    private readonly RuleSet _rules;

    public ActionValidator(RuleSet rules)
    {
        _rules = rules;
    }

    public Result Validate(PlayerActionContext context, CombatActionChoice req)
    {
        // utiliser CanActSpecification, CanTargetSpecification, Energy, Cooldown, etc.
        var t = _rules.Evolution;
        return Result.Ok();
    }
}
