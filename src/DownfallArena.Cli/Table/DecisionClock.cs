using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// When each seat's current question was put in front of a person. It is what <c>elapsedMs</c> is measured
/// from (<c>docs/tabletop/playtest-app.md</c>, 5.3).
/// </summary>
/// <remarks>
/// The moment that counts is the moment the options were <em>served</em>, not the moment the engine asked
/// them: the driver may ask a seat while nobody is looking at the screen, and a duration that includes the
/// walk back to the table measures the room rather than the decision. So the first poll that carries a
/// question stamps it, later polls of the same question leave the stamp alone, and answering it clears it.
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
    /// The moment the question a seat just answered was served, and forgets it. A decision that arrived
    /// without the options ever being served is timed at zero rather than against the wall clock: it means
    /// nobody read a screen, which is a scripted seat and not a person taking no time.
    /// </summary>
    public DateTimeOffset Answered(PlayerSlot slot)
    {
        lock (_gate)
        {
            var served = _served.Remove(slot, out var stamped) ? stamped.At : clock.GetUtcNow();
            return served;
        }
    }

    private sealed record Stamped(HumanSeat.Question Question, DateTimeOffset At);
}
