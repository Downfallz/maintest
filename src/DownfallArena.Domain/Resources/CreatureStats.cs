using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Resources;

/// <summary>
/// A creature's stat block: the base values a creature definition starts a match with.
/// </summary>
public sealed record CreatureStats(
    Health Health,
    Energy Energy,
    Defense Defense,
    Initiative Initiative,
    CriticalChance CriticalChance);
