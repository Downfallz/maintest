using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>
/// Damage a creature took from its bleed conditions at the start of a round.
/// </summary>
public sealed record BleedTick(CreatureId Creature, int Damage, IReadOnlyList<ConditionShare> Shares)
{
    /// <summary>The tick with nothing attributed, for a caller that has no spell to name.</summary>
    public BleedTick(CreatureId creature, int damage)
        : this(creature, damage, [])
    {
    }
}
