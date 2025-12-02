using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.Policies.MatchPhase;
using DA.Game.Domain2.Matches.Policies.Planning;
using DA.Game.Domain2.Matches.Services.Combat;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution.Execution;
using DA.Game.Domain2.Matches.Services.Planning;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.RuleSets;

public static class RuleSetV1
{
    public static RuleSet Create()
    {
        // --- Phase Rules
        var phase = new PhaseRules(new MatchPhasePolicyV1());

        // --- Evolution Rules
        var evolution = new PlanningRules(new CombatTimelineBuilderServiceV1(),
            new InitiativePolicyV1());

        //// --- Speed Rules
        //var speed = new SpeedRules(
        //    SpeedChoicePolicy: new SpeedChoicePolicyV1(),
        //    TimelineBuilder: new TimelineBuilderV1());

        // --- Combat Rules
        var combat = new CombatRules(
            new IntentValidationService(new CombatActionSelectionPolicyV1(), new CostPolicyV1()),
                new AttackChoiceValidationService(
                new CombatActionSelectionPolicyV1(), 
                new CostPolicyV1(), 
                new TargetingPolicyV1()),
            new CombatActionResolutionService(
                new CombatActionResolutionPolicyV1(),
                new CostPolicyV1(),
                new TargetingPolicyV1(),
                new EffectComputationServiceV1(),
                new CritComputationServiceV1(new SystemRandom())),
            new CombatActionExecutionService(new InstantEffectService(new DamageComputationService())));

        //// --- Status Rules
        //var status = new StatusRules(
        //    DurationPolicy: new StatusDurationPolicyV1(),
        //    StackingPolicy: new StatusStackingPolicyV1());

        // --- Assemble the full rulebook
        return new RuleSet(phase, evolution, combat);
    }
}
