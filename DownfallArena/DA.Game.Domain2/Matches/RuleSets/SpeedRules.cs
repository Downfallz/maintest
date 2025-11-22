using DA.Game.Domain2.Matches.Policies.Speed;

namespace DA.Game.Domain2.Matches.RuleSets;

public sealed record SpeedRules(
    IInitiativePolicy Policy);
