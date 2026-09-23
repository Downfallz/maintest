using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// The players' tie orders were applied, and this is the timeline the round is played in. Raised only when a
/// player had a tie to order; otherwise the timeline of <see cref="TimelineBuilt"/> stands.
/// </summary>
public sealed record TiesOrdered(MatchId MatchId, RoundId RoundId, CombatTimeline Timeline) : IMatchEvent;
