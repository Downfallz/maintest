using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

public sealed record EvolutionOption(CreatureId Creature, IReadOnlyList<SpellId> UnlockableSpells)
{
    public bool Equals(EvolutionOption? other) =>
        other is not null && Creature == other.Creature && UnlockableSpells.SequenceEqual(other.UnlockableSpells);

    public override int GetHashCode() => HashCode.Combine(Creature, UnlockableSpells.Count);
}
