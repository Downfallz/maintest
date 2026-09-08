using DA.Game.Domain2.Matches.RuleSets;

namespace DA.Game.Application.Shared.RuleSets;

public interface IRuleSetProvider
{
    RuleSet Current { get; }
}
