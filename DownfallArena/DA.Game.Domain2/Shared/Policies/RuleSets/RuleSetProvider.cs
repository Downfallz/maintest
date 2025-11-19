namespace DA.Game.Domain2.Shared.Policies.RuleSets;

public sealed class RuleSetProvider : IRuleSetProvider
{
    public RuleSet Current { get; }

    public RuleSetProvider()
    {
        Current = RuleSetV1.Create();
    }
}