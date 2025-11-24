using DA.Game.Domain2.Matches.Policies.Evolution;
using DA.Game.Domain2.Matches.Policies.MatchPhase;
using DA.Game.Domain2.Matches.Services.Combat;
using DA.Game.Domain2.Matches.Services.Combat.Execution;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;

namespace DA.Game.Domain2.Matches.RuleSets;

public static class RuleSetV1
{
    public static RuleSet Create()
    {
        // --- Phase Rules
        var phase = new PhaseRules(new MatchPhasePolicyV1());

        // --- Evolution Rules
        var evolution = new EvolutionRules(new EvolutionPolicy());

        //// --- Speed Rules
        //var speed = new SpeedRules(
        //    SpeedChoicePolicy: new SpeedChoicePolicyV1(),
        //    TimelineBuilder: new TimelineBuilderV1());

        // --- Combat Rules
        var combat = new CombatRules(new CombatActionResolutionService(new EffectComputationServiceV1()),
            new EffectExecutionService(new InstantEffectServiceV1(new DamageComputationService())));

        //// --- Status Rules
        //var status = new StatusRules(
        //    DurationPolicy: new StatusDurationPolicyV1(),
        //    StackingPolicy: new StatusStackingPolicyV1());

        // --- Assemble the full rulebook
        return new RuleSet(phase, evolution, combat);
    }
}
