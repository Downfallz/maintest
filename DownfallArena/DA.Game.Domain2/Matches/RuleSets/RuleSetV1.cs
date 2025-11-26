using DA.Game.Domain2.Matches.Policies.Combat;
using DA.Game.Domain2.Matches.Policies.Evolution;
using DA.Game.Domain2.Matches.Policies.MatchPhase;
using DA.Game.Domain2.Matches.Services.Combat;
using DA.Game.Domain2.Matches.Services.Combat.Execution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.RuleSets;

public static class RuleSetV1
{
    public static RuleSet Create()
    {
        // --- Phase Rules
        var phase = new PhaseRules(new MatchPhasePolicyV1());

        // --- Evolution Rules
        var evolution = new EvolutionRules();

        //// --- Speed Rules
        //var speed = new SpeedRules(
        //    SpeedChoicePolicy: new SpeedChoicePolicyV1(),
        //    TimelineBuilder: new TimelineBuilderV1());

        // --- Combat Rules
        var combat = new CombatRules(new AttackChoiceValidationService(
                new AttackChoicePolicyV1(), 
                new CostPolicyV1(), 
                new TargetingPolicyV1()),
            new CombatActionResolutionService(
                new CombatActionResolutionPolicyV1(),
                new CostPolicyV1(),
                new TargetingPolicyV1(),
                new EffectComputationServiceV1(),
                new CritComputationService(new SystemRandom())),
            new EffectExecutionService(new InstantEffectService(new DamageComputationService())));

        //// --- Status Rules
        //var status = new StatusRules(
        //    DurationPolicy: new StatusDurationPolicyV1(),
        //    StackingPolicy: new StatusStackingPolicyV1());

        // --- Assemble the full rulebook
        return new RuleSet(phase, evolution, combat);
    }
}
