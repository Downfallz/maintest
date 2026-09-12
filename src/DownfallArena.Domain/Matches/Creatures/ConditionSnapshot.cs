using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Domain.Matches.Creatures;

/// <summary><see cref="Source"/> is the cast the condition came from, or refreshed it last (ADR 0027).</summary>
public sealed record ConditionSnapshot(LastingEffect Effect, int? RemainingRounds, ConditionSource? Source = null);
