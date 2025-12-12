using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Services.Phases;

public sealed record CombatActionGateResult(
    bool CanAdvance,
    int RemainingReveals,
    CreatureId? NextActorId
);