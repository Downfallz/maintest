using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Services.Combat.Execution;

// ------------------------------------------------------------
// DTO / helper types used inside the service
// ------------------------------------------------------------

public sealed record InstantEffectApplication(
    CreatureId ActorId,
    CreatureId TargetId,
    EffectKind Kind,
    int Amount
);
