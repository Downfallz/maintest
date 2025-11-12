using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Policies.RuleSets;
using DA.Game.Shared;

namespace DA.Game.Domain2.Services;

public sealed class EffectPipeline
{
    private readonly IEnumerable<IEffectStep> _steps;

    public EffectPipeline(IEnumerable<IEffectStep> steps)
    {
        _steps = steps;
    }

    public CombatActionResult Execute(CombatActionIntent intent, RuleSet rules, IClock clock)
    {
        // accumulateur
        var result = new CombatActionResult(intent.ActorId, intent.SpellId, new List<EffectSummary>(), true);
        foreach (var step in _steps)
            result = step.Execute(result, intent, rules, clock);
        return result;
    }
}
