using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Domain.Matches.Creatures;

public sealed record ConditionSnapshot(LastingEffect Effect, int? RemainingRounds);
