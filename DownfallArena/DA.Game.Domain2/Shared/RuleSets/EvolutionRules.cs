using DA.Game.Domain2.Shared.Policies.Evolution;

namespace DA.Game.Domain2.Shared.RuleSets;

public sealed record EvolutionRules(
    IEvolutionPolicy Evolution);
