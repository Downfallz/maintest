using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>What one creature's energy regeneration conditions gave it at the start of a round.</summary>
public sealed record EnergyRegenerationTick(CreatureId Creature, int Gained, IReadOnlyList<ConditionShare> Shares)
{
    /// <summary>The tick with nothing attributed, for a caller that has no spell to name.</summary>
    public EnergyRegenerationTick(CreatureId creature, int gained)
        : this(creature, gained, [])
    {
    }
}
