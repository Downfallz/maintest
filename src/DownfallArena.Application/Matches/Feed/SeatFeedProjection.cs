using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Feed;

/// <summary>
/// What one seat may be told of what has happened so far: the events it is allowed to see
/// (<see cref="SeatVisibility" />), each without the boards the trace keeps beside it.
/// </summary>
public static class SeatFeedProjection
{
    /// <summary>
    /// The entries from <paramref name="since" /> onwards, in the order they happened.
    /// <paramref name="since" /> is a sequence number and not a count: a client asks for what it has not seen
    /// by passing one past its last, and asking twice for the same number twice answers the same thing.
    /// Sequence numbers are the trace's, so they do not close up when an event is filtered out — a seat that
    /// cannot see an event sees the gap where it was, which is what lets it ask for the next one.
    /// </summary>
    public static IReadOnlyList<SeatFeedEntry> Build(IReadOnlyList<TraceEntry> entries, PlayerSlot slot, int since = 0)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return
        [
            .. entries
                .Where(entry => entry.Sequence >= since && SeatVisibility.CanSee(entry.Event, slot))
                .Select(entry => new SeatFeedEntry(entry.Sequence, entry.Round, entry.SubPhase, entry.Event)),
        ];
    }
}
