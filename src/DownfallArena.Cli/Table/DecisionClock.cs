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
    /// When this question was put in front of the person, or <c>null</c> if it never was.
    /// </summary>
    /// <remarks>
    /// Read before the decision is handed over, and that is the whole reason this is separate from
    /// <see cref="Answered" />. Submitting releases the driver, which can ask the seat the next question and
    /// have a poll stamp it before the host has finished writing the first one down -- and a stamp is one per
    /// seat, so the new question's stamp replaces the answered one's. Reading it first means the race can cost
    /// the *next* duration a poll of accuracy, and never costs this one its whole duration.
    /// </remarks>
    public DateTimeOffset? ServedAt(PlayerSlot slot, HumanSeat.Question? question)
    {
        lock (_gate)
        {
            return question is not null && _served.TryGetValue(slot, out var stamped) && stamped.Question == question
                ? stamped.At
                : null;
        }
    }

    /// <summary>
    /// Forgets the stamp of the question a seat has answered, so whatever it is asked next is timed from its
    /// own serving. The question has to be named: if the next one has already been stamped, that stamp is the
    /// next one's and taking it would leave that decision timed from nothing.
    /// </summary>
    public void Answered(PlayerSlot slot, HumanSeat.Question? answered)
    {
        lock (_gate)
        {
            if (answered is not null && _served.TryGetValue(slot, out var stamped) && stamped.Question == answered)
            {
                _served.Remove(slot);
            }
        }
    }

    private sealed record Stamped(HumanSeat.Question Question, DateTimeOffset At);
}
