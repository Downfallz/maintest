using DA.Game.Domain2.Shared.Policies.Evolution;

namespace DA.Game.Domain2.Shared.Policies.RuleSets;

public sealed record SpeedRules(
    IInitiativePolicy Policy);
