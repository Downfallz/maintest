using DA.Game.Domain2.Shared.Policies.MatchPhase;

namespace DA.Game.Domain2.Shared.Policies.RuleSets;

public sealed record PhaseRules(
    IMatchPhasePolicy MatchPhase);
