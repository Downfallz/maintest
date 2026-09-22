using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>One creature and the packages it may buy now (ADR 0056).</summary>
public sealed record EvolutionOption(CreatureId Creature, IReadOnlyList<TierId> AvailableTiers)
{
    public bool Equals(EvolutionOption? other) =>
        other is not null && Creature == other.Creature && AvailableTiers.SequenceEqual(other.AvailableTiers);

    public override int GetHashCode() => HashCode.Combine(Creature, AvailableTiers.Count);
}
