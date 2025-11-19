using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.RuleSets;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Services;

public sealed class EffectPipeline
{
    private readonly IEnumerable<IEffectStep> _steps;

    public EffectPipeline(IEnumerable<IEffectStep> steps)
    {
        _steps = steps;
    }

    public CombatActionResult Execute(CombatActionChoice intent, RuleSet rules, IClock clock)
    {
        // accumulateur
        var result = new CombatActionResult(intent, new List<EffectSummary>());
        foreach (var step in _steps)
            result = step.Execute(result, intent, rules, clock);
        return result;
    }
}
