using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// A position in the combat timeline at which one creature acts.
/// </summary>
public sealed record ActivationSlot(PlayerSlot Owner, CreatureId Creature, Speed Speed, Initiative Initiative)
{
    /// <summary>Whether two slots tie: the same band and the same initiative, whoever holds them (ADR 0063).</summary>
    public bool TiesWith(ActivationSlot other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Speed == other.Speed && Initiative == other.Initiative;
    }
}
