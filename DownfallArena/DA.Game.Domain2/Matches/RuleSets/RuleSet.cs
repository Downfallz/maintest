namespace DA.Game.Domain2.Matches.RuleSets;

public sealed record RuleSet(PhaseRules Phase,
    PlanningRules Planning,
    CombatRules Combat,
    ConditionRules Conditions);
