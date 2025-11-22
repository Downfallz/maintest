namespace DA.Game.Domain2.Matches.RuleSets;

public sealed record RuleSet(PhaseRules Phase,
    EvolutionRules Evolution,
    CombatRules Combat);
