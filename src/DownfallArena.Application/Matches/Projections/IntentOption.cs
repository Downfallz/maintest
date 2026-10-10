using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// One creature to declare for: the spells it can pay for now, and the ones it knows but cannot pay for yet.
/// </summary>
/// <remarks>
/// Both may be declared (ADR 0106): the price is asked when the slot comes up, and energy can arrive in between
/// from an ally acting earlier, or not, and then the cast fizzles. The agents choose among the castable ones
/// only, which keeps their play, and every recorded match, what it was; the second list is for a person who
/// plans the round the agents do not.
/// </remarks>
public sealed record IntentOption(CreatureId Creature, IReadOnlyList<SpellId> CastableSpells)
{
    public IntentOption(CreatureId creature, IReadOnlyList<SpellId> castableSpells, IReadOnlyList<SpellId> unaffordableSpells)
        : this(creature, castableSpells)
    {
        UnaffordableSpells = unaffordableSpells;
    }

    /// <summary>Spells the creature knows and cannot pay for now: declared, they fizzle unless the energy arrives first.</summary>
    public IReadOnlyList<SpellId> UnaffordableSpells { get; init; } = [];

    /// <summary>Whether a person may declare this spell for the creature: castable now, or not yet.</summary>
    public bool Offers(SpellId? spell) => spell is not null && (CastableSpells.Contains(spell) || UnaffordableSpells.Contains(spell));

    public bool Equals(IntentOption? other) =>
        other is not null && Creature == other.Creature && CastableSpells.SequenceEqual(other.CastableSpells)
        && UnaffordableSpells.SequenceEqual(other.UnaffordableSpells);

    public override int GetHashCode() => HashCode.Combine(Creature, CastableSpells.Count, UnaffordableSpells.Count);
}
