using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

public sealed record IntentOption(CreatureId Creature, IReadOnlyList<SpellId> CastableSpells)
{
    public bool Equals(IntentOption? other) =>
        other is not null && Creature == other.Creature && CastableSpells.SequenceEqual(other.CastableSpells);

    public override int GetHashCode() => HashCode.Combine(Creature, CastableSpells.Count);
}
