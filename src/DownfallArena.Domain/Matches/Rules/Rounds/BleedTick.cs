using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>
/// Damage a creature took from its bleed conditions at the start of a round.
/// </summary>
public sealed record BleedTick(CreatureId Creature, int Damage);
