using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Creatures;

/// <summary>
/// Who a condition came from: the creature that cast the spell, and the spell it cast (ADR 0027).
/// <para>
/// The caster is carried beside the spell because the spell alone cannot say whose tally an upkeep tick
/// belongs on -- in a mirrored evaluation both sides cast the same spell id, and a regeneration is cast on
/// an ally while a bleed is cast on an enemy, so the bleeding creature's own side is no guide either.
/// </para>
/// </summary>
public sealed record ConditionSource(CreatureId Caster, SpellId Spell);
