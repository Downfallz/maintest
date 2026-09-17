using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Domain.Matches.Creatures;

/// <summary>
/// <see cref="Source"/> is the cast the condition came from, or refreshed it last (ADR 0027). <see cref="IsFresh"/>
/// says whether the condition still has its first countdown ahead of it, the one that does not count: two
/// conditions with the same remaining rounds expire a round apart depending on it, so a snapshot that leaves it
/// out cannot be restored into a condition that expires when the original would (ADR 0047). Left out, it
/// describes a condition past that first countdown; a condition just applied is built with it set.
/// </summary>
public sealed record ConditionSnapshot(LastingEffect Effect, int? RemainingRounds, ConditionSource? Source = null, bool IsFresh = false);
