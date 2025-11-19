using DA.Game.Domain2.Shared.RuleSets;

namespace DA.Game.Application.Shared.RuleSets;

public sealed class RuleSetProvider : IRuleSetProvider
{
    public RuleSet Current { get; }

    public RuleSetProvider()
    {
        Current = RuleSetV1.Create();
    }
}