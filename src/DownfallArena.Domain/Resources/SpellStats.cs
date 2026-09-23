using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.Domain.Resources;

/// <summary>
/// The numbers that drive a spell in combat: what it costs, and how often it crits.
/// </summary>
/// <remarks>
/// A spell used to carry an acquisition initiative as well, raised on its learner's base initiative when it
/// was unlocked (ADR 0017). A pick buys a package now, and the package pays that bonus once (ADR 0056), so
/// the per-spell number stopped being read by any rule. It is gone rather than left at zero: a stat nothing
/// consults still has to be authored, tuned and explained.
/// </remarks>
public sealed record SpellStats(Energy Cost, CriticalChance CriticalChance);
