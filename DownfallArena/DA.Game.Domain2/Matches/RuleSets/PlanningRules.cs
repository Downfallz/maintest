using DA.Game.Domain2.Matches.Policies.Planning;

namespace DA.Game.Domain2.Matches.RuleSets;

public sealed record PlanningRules(IInitiativePolicy Policy);
