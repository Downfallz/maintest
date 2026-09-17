using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// When each seat's current question was put in front of a person. It is what <c>elapsedMs</c> is measured
/// from (<c>docs/tabletop/playtest-app.md</c>, 5.3).
/// </summary>
/// <remarks>
/// The moment that counts is the moment the options were <em>served to somebody looking at them</em>, not the
/// moment the engine asked them. The driver may ask a seat while nobody is looking at the screen, and in
/// hotseat the page polls both seats on a timer while only one of them is on screen and the other is behind
/// the pass-the-device screen: a duration started by a background poll would measure the handover -- the walk
/// round the table, picking the phone up, tapping ready -- and not the decision. So only a poll that says it
/// is rendering that seat stamps it, later polls of the same question leave the stamp alone, and answering it
/// takes it away.
/// </remarks>
internal sealed class DecisionClock(TimeProvider clock)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<PlayerSlot, Stamped> _served = [];

    /// <summary>
    /// Records that a seat has been shown what it is being asked. A seat waiting for nothing is forgotten,
    /// which is what makes the next question a new one rather than a continuation of the last.
    /// </summary>
    public void Served(PlayerSlot slot, HumanSeat.Question? question)
    {
        lock (_gate)
        {
            if (question is null)
            {
                _served.Remove(slot);
                return;
            }

            if (_served.TryGetValue(slot, out var already) && already.Question == question)
            {
                return;
            }

            _served[slot] = new Stamped(question, clock.GetUtcNow());
        }
    }

    /// <summary>
    /// The moment the question a seat just answered was served, and forgets it.
    /// </summary>
    /// <remarks>
    /// The question has to be named, and not merely the seat. A decision releases the driver before the host
    /// has finished writing it down, so the engine can have asked the next question -- and a poll can have
    /// stamped it -- before this is reached. Taking whatever stamp is there would then time this decision at
    /// nothing and leave the next one with no stamp at all, so it would be timed short too: one race, two
    /// wrong durations. When the stamp is not this decision's it is left where it is and this one is timed at
    /// zero, which loses one duration and keeps the next honest. Zero is also the answer when the options were
    /// never served to anybody, which means a scripted seat rather than a person who took no time.
    /// </remarks>
    public DateTimeOffset Answered(PlayerSlot slot, HumanSeat.Question? answered)
    {
        lock (_gate)
        {
            if (answered is not null && _served.TryGetValue(slot, out var stamped) && stamped.Question == answered)
            {
                _served.Remove(slot);
                return stamped.At;
            }

            return clock.GetUtcNow();
        }
    }

    private sealed record Stamped(HumanSeat.Question Question, DateTimeOffset At);
}
