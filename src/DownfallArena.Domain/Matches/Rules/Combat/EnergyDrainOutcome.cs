using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Energy taken from a creature. Separate from <see cref="EnergyOutcome"/> rather than a signed amount,
/// because nothing in this taxonomy is signed: <see cref="DamageOutcome"/> and <see cref="HealOutcome"/> are
/// two records for the same reason (ADR 0035).
/// </summary>
public sealed record EnergyDrainOutcome(CreatureId Target, int Amount) : EffectOutcome(Target);
