using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Damage after the critical multiplier and the target's total defense, floored at zero: to deal in a
/// resolution's outcomes, dealt in the applied ones, where it is capped by the health the target had left.
/// </summary>
public sealed record DamageOutcome(CreatureId Target, int Amount, bool Critical) : EffectOutcome(Target)
{
    /// <summary>
    /// What the target's defense took from the hit: the hit before defense less <see cref="Amount" />, so the two
    /// add up to the hit the critical left. Set where the rules compute the hit, and kept by the applied outcome,
    /// whose amount the target's health may cap further. A hit the defense took whole deals nothing, so it is in a
    /// resolution's outcomes and not in the applied ones.
    /// </summary>
    public int Blocked { get; init; }
}
