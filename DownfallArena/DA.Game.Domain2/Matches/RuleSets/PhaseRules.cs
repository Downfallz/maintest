using DA.Game.Domain2.Matches.Policies.MatchPhase;

namespace DA.Game.Domain2.Matches.RuleSets;

public sealed record PhaseRules(
    IMatchPhasePolicy MatchPhase);
