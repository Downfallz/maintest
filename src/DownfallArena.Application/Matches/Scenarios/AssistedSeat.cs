using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// The person's seat in a Tutorial, for its Assisted questions (ADR 0187): a question of a kind the lesson does
/// not teach is answered from the source's recorded answers for that seat, each checked as the scripted
/// opponent checks its own (same kind, same creature, among the options). At the first answer that does not fit,
/// or when none is left, every question goes back to the person for good.
/// </summary>
/// <remarks>
/// The recorded answers hold the taught kinds too, which the person answers as they like. Those at the head of
/// the script are passed over when an assisted question comes: they were the person's to give.
/// </remarks>
public sealed class AssistedSeat
{
    private readonly TutorialLesson _lesson;
    private readonly Queue<PlayerDecision> _script;

    /// <param name="lesson">The lesson, which says the question kinds the person answers.</param>
    /// <param name="recorded">The seat's recorded answers after the cut, of every kind, in order.</param>
    public AssistedSeat(TutorialLesson lesson, IEnumerable<PlayerDecision> recorded)
    {
        _lesson = lesson ?? throw new ArgumentNullException(nameof(lesson));
        _script = new Queue<PlayerDecision>(recorded ?? throw new ArgumentNullException(nameof(recorded)));
    }

    /// <summary>Whether the recorded answers stopped fitting and the person answers every question from now on.</summary>
    public bool HandedBack { get; private set; }

    /// <summary>Said once, when the seat goes back to the person.</summary>
    public event EventHandler? GaveBack;

    /// <summary>The seat's recorded answers after a scenario's cut, given for the kinds the lesson does not teach.</summary>
    public static AssistedSeat Of(ScenarioCut scenario, PlayerSlot seat, TutorialLesson lesson)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return new AssistedSeat(lesson, scenario.After.OfType<DecisionEntry>().Where(entry => entry.Decision.Slot == seat).Select(entry => entry.Decision.Decision));
    }

    /// <summary>
    /// The recorded answer to the question the seat is asked, or none when the question is the person's: nothing
    /// is asked, the lesson teaches its kind, or the seat was handed back. The answer stays on the script until
    /// <see cref="Given" /> says the match took it, so asking again gives the same one.
    /// </summary>
    public PlayerDecision? Answer(SeatView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (HandedBack || view.Asked is not { } asked || !_lesson.Assists(asked.Kind))
        {
            return null;
        }

        while (_script.TryPeek(out var passed) && !_lesson.Assists(passed.Kind))
        {
            _script.Dequeue();
        }

        if (_script.TryPeek(out var next) && asked.Answers(next) && PlayerDecisionCheck.Validate(view.Options, next).IsSuccess)
        {
            return next;
        }

        HandBack();
        return null;
    }

    /// <summary>
    /// Gives every question back to the person for good, as an answer that does not fit does: for whoever submits
    /// the answers, when the match did not take one. The person answered it first, or it could not be written,
    /// and the script no longer says what the seat did.
    /// </summary>
    public void HandBack()
    {
        if (HandedBack)
        {
            return;
        }

        HandedBack = true;
        _script.Clear();
        GaveBack?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Says the match took the answer <see cref="Answer" /> last gave: it is taken off the script.</summary>
    public void Given()
    {
        if (!HandedBack && _script.TryPeek(out var next) && _lesson.Assists(next.Kind))
        {
            _script.Dequeue();
        }
    }
}
