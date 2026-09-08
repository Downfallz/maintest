using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// A player's decision to unlock a spell for a creature during the Evolution sub-phase.
/// </summary>
public sealed record EvolutionChoice(CreatureId Creature, SpellId Spell);
