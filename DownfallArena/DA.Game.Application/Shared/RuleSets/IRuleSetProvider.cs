using DA.Game.Domain2.Shared.RuleSets;

namespace DA.Game.Application.Shared.RuleSets;

public interface IRuleSetProvider
{
    RuleSet Current { get; }
}
