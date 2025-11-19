using DA.Game.Domain2.Shared.Policies.MatchPhase;

namespace DA.Game.Domain2.Shared.RuleSets;

public sealed record PhaseRules(
    IMatchPhasePolicy MatchPhase);
