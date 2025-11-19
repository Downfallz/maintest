using DA.Game.Domain2.Shared.Policies.Speed;

namespace DA.Game.Domain2.Shared.RuleSets;

public sealed record SpeedRules(
    IInitiativePolicy Policy);
