using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Damage after the critical multiplier and the target's total defense, floored at zero: to deal in a
/// resolution's outcomes, dealt in the applied ones, where it is capped by the health the target had left.
/// </summary>
public sealed record DamageOutcome(CreatureId Target, int Amount, bool Critical) : EffectOutcome(Target);
