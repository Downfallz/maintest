using DownfallArena.Domain.Matches.Events;
using DownfallArena.Domain.Matches.Rounds;

namespace DownfallArena.Application.Matches.Feed;

/// <summary>
/// One thing that happened, as a seat is told it: where it sits in the match, and the event itself.
/// </summary>
/// <remarks>
/// What it does not carry is the point. The trace entry this is built from holds both players' boards
/// (<c>TraceEntry</c>), which is what makes a trace worth reading afterwards and what makes it unservable
/// during a session.
/// </remarks>
public sealed record SeatFeedEntry(int Sequence, int? Round, RoundSubPhase? SubPhase, IMatchEvent Event);
