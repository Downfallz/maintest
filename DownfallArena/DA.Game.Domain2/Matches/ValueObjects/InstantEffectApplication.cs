using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record InstantEffectApplication(
    CreatureId TargetId,
    EffectKind Kind,   // Damage, Heal, Energy, Defense, Initiative, Retaliate, Stun, Crit, etc.
    int Amount
// éventuellement IsCritical, WasBlocked, etc.
);
