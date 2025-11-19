using DA.Game.Domain2.Services;
using DA.Game.Domain2.Shared.Policies.Evolution;
using DA.Game.Domain2.Shared.Policies.MatchPhase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Domain2.Shared.RuleSets;

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

        //// --- Combat Rules
        //var combat = new CombatRules(
        //    ActionValidator: new ActionValidatorV1(),
        //    ActionResolver: new ActionResolverV1());

        //// --- Status Rules
        //var status = new StatusRules(
        //    DurationPolicy: new StatusDurationPolicyV1(),
        //    StackingPolicy: new StatusStackingPolicyV1());

        // --- Assemble the full rulebook
        return new RuleSet(phase, evolution);
    }
}
