using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// What a player answers to <see cref="ActionOptions" />: one spell and its targets (ADR 0110).
/// </summary>
public sealed record ChosenAction(SpellId Spell, IReadOnlyList<CreatureId> Targets)
{
    public bool Equals(ChosenAction? other) =>
        other is not null && Spell == other.Spell && Targets.SequenceEqual(other.Targets);

    public override int GetHashCode() => HashCode.Combine(Spell, Targets.Count);
}
