using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Domain2.Matches.Entities.Conditions;

public sealed record ConditionInstance
{
    public required ConditionKind Kind { get; init; }
    public required StackPolicy StackPolicy { get; init; }
    public required ConditionPhase Phase { get; init; }

    public EffectKind? SourceEffect { get; init; } // Bleed / Poison / Burn (optional)

    /// <summary>
    /// Modifier applied at the specified phase.
    /// For DamageOverTime, Modifier is the damage-per-tick (positive).
    /// For buffs/debuffs, Modifier is the delta applied to the derived stat.
    /// </summary>
    public required int Modifier { get; init; }

    /// <summary>
    /// Remaining rounds. -1 means permanent.
    /// </summary>
    public required int RemainingRounds { get; init; }

    public bool IsPermanent => RemainingRounds < 0;
    public bool IsExpired => !IsPermanent && RemainingRounds <= 0;

    public ConditionInstance Tick()
        => IsPermanent ? this : this with { RemainingRounds = RemainingRounds - 1 };
}
