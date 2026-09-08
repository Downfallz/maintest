using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The combat timeline of the round was built from the speed choices.
/// </summary>
public sealed record TimelineBuilt(MatchId MatchId, RoundId RoundId, CombatTimeline Timeline) : IMatchEvent;
