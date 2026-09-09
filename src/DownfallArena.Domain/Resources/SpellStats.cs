using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Resources;

/// <summary>
/// The numbers that drive a spell in combat: when it acts, what it costs, how often it crits.
/// </summary>
public sealed record SpellStats(Initiative SpellInitiative, Energy Cost, CriticalChance CriticalChance);
