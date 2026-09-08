using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Damage to deal after the critical multiplier and the target's total defense, floored at zero.
/// </summary>
public sealed record DamageOutcome(CreatureId Target, int Amount, bool Critical) : EffectOutcome(Target);
