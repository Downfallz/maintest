using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// A hidden declaration of the spell a creature will use in its activation slot.
/// </summary>
public sealed record CombatIntent(CreatureId Actor, SpellId Spell);
