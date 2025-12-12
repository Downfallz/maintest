using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Policies.MatchPhase;
using DA.Game.Domain2.Matches.Services.Phases;
using DA.Game.Shared.Contracts.Resources;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.RuleSets;

public class PhaseRules(
    IMatchPhasePolicy matchPhase,
    ISpeedProgressionEvaluatorService speedEvaluator,
    IEvolutionProgressionEvaluatorService evolutionEvaluator,
    ICombatPlanningProgressionEvaluatorService combatPlanningEvaluator,
    ICombatActionProgressionEvaluatorService combatActionEvaluator)
{
    public Result<EvolutionGateResult> EvaluateEvolutionPhaseCompleted(Match match, IGameResources resources)
    {
        return evolutionEvaluator.Evaluate(match, resources);
    }

    public Result<SpeedGateResult> EvaluateSpeedPhaseCompleted(Match match)
    {
        return speedEvaluator.Evaluate(match);
    }

    public Result<CombatPlanningGateResult> EvaluateCombatPlanningPhaseCompleted(Match match)
    {
        return combatPlanningEvaluator.Evaluate(match);
    }

    public Result<CombatActionGateResult> EvaluateCombatActionPhaseCompleted(Match match)
    {
        return combatActionEvaluator.Evaluate(match);
    }

    public Result EnsureCanSubmitSpeedChoice(Match match)
    {
        return matchPhase.EnsureCanSubmitSpeedChoice(match);
    }

    public Result EnsureCanSubmitEvolutionChoice(Match match)
    {
        return matchPhase.EnsureCanSubmitEvolutionChoice(match);
    }

    public Result EnsureCanSubmitCombatAction(Match match)
    {
        return matchPhase.EnsureCanSubmitCombatAction(match);
    }
}
