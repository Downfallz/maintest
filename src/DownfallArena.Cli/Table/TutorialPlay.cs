using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Scenarios;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A Tutorial being played at the host (ADR 0187, docs/tabletop/onboarding-plan.md, O3a): it answers the person's
/// Assisted questions from the recorded answers, one after the other as the match asks them, and holds what the
/// seat's view says of the lesson: its steps, how many the person was asked the question of, and whether it is done.
/// </summary>
/// <remarks>
/// An assisted answer is submitted as the person's own, through the gate any decision goes through, so the seat
/// stays its person's and a question handed back needs no seat change. Between the question asked and its answer
/// the seat's view says the question; <see cref="Assisted" /> tells the page which kinds it is not to draw.
/// </remarks>
internal sealed class TutorialPlay
{
    private readonly TutorialLesson _lesson;
    private readonly AssistedSeat _assisted;
    private readonly PlayerSlot _seat;
    private readonly Lock _gate = new();
    private readonly bool[] _shown;
    private Seat? _table;
    private bool _done;
    private int _answering;
    private int _again;

    public TutorialPlay(TutorialLesson lesson, AssistedSeat assisted, PlayerSlot seat, int of)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        ArgumentNullException.ThrowIfNull(assisted);
        _lesson = lesson;
        _assisted = assisted;
        _seat = seat;
        _shown = new bool[lesson.Steps.Count];
        Of = Math.Max(of, lesson.Order);
        Teaches = [.. lesson.Teaches.Select(kind => kind.ToString())];
        Steps = [.. lesson.Steps.Select(step => new Step(
            new Question(step.Kind.ToString(), step.Round, step.Creature?.Value),
            step.Says,
            step.Concepts,
            step.PointsAt is { } pointer ? new Pointer(pointer.Creature?.Value, pointer.Control) : null))];
    }

    /// <summary>Its place in the series, from 1.</summary>
    public int Order => _lesson.Order;

    /// <summary>How many tutorials the series has at this host.</summary>
    public int Of { get; }

    public string Title => _lesson.Title;

    /// <summary>The question kinds the person answers.</summary>
    public IReadOnlyList<string> Teaches { get; }

    /// <summary>The question kinds still answered for the person, which the page does not draw; none once the seat was handed back.</summary>
    public IReadOnlyList<string> Assisted
    {
        get
        {
            lock (_gate)
            {
                return _assisted.HandedBack ? [] : [.. Kinds.Where(_lesson.Assists).Select(kind => kind.ToString())];
            }
        }
    }

    public IReadOnlyList<Step> Steps { get; }

    /// <summary>How many steps the person was asked the question of.</summary>
    public int Shown
    {
        get
        {
            lock (_gate)
            {
                return _shown.Count(shown => shown);
            }
        }
    }

    /// <summary>Whether the last step was shown and its round ended, or the match ended, win or lose.</summary>
    public bool Done
    {
        get
        {
            lock (_gate)
            {
                return _done;
            }
        }
    }

    private static readonly PlayerOptionsKind[] Kinds =
        [PlayerOptionsKind.Evolution, PlayerOptionsKind.Speed, PlayerOptionsKind.TieOrder, PlayerOptionsKind.Intent, PlayerOptionsKind.Target, PlayerOptionsKind.Action];

    /// <summary>A step as the page reads it.</summary>
    internal sealed record Step(Question At, IReadOnlyList<string> Says, IReadOnlyList<string> Concepts, Pointer? PointsAt);

    internal sealed record Question(string Kind, int Round, int? Creature);

    internal sealed record Pointer(int? Creature, string? Control);

    /// <summary>
    /// Said once, when every question goes back to the person: what the seat's view says of the lesson changed
    /// without the match moving, so whoever tells the pages a table changed is told here. Said on the thread that
    /// handed back, outside this play's lock: a subscriber may take its own lock and read the play.
    /// </summary>
    public event Action? GaveBack;

    /// <summary>What this reads and answers of a practice's table: the person's seat as it stands, whether the match is over, and the gate a decision goes through.</summary>
    internal sealed record Seat(Func<SeatSnapshot?> Read, Func<bool> Over, Func<Application.Matches.Decisions.PlayerDecision, long, Task<DecisionTaken>> Decide);

    /// <summary>Follows a practice's session from now on: every change of what the seats read is looked at.</summary>
    /// <remarks>
    /// A stepped session and no other: it answers a read from what it keeps, so looking, which happens inside
    /// the request that changed the match, waits for nothing.
    /// </remarks>
    public void Follow(SteppedSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Follow(new Seat(
            () => session.SnapshotAsync(_seat).GetAwaiter().GetResult() is { IsSuccess: true } read ? read.Value : null,
            () => session.IsOver,
            (decision, asked) => session.DecideAsync(_seat, decision, asked)));
        session.Changed += Look;
    }

    internal void Follow(Seat table) => _table = table;

    /// <summary>
    /// Runs <paramref name="step" /> on the assisted seat under the gate, and says whether that step is the one that
    /// handed the seat back. The hand-back is said by <see cref="SayGaveBack" /> once the gate is left: its
    /// subscribers bump the table, whose own subscribers may take their locks and read this play (#658).
    /// </summary>
    private bool HandsBack(Action step)
    {
        lock (_gate)
        {
            var before = _assisted.HandedBack;
            step();
            return !before && _assisted.HandedBack;
        }
    }

    private void SayGaveBack()
    {
        Console.WriteLine($"  Tutorial {Order}: the recorded answers no longer fit the match; the person answers every question from here.");
        GaveBack?.Invoke();
    }

    /// <summary>
    /// Reads what the person's seat is asked now: notes the step it shows and whether the tutorial is done, and has
    /// an assisted question answered. Called by the session on every change, and once when the practice begins.
    /// </summary>
    public void Look()
    {
        if (_table is not { } table || table.Read() is not { } snapshot)
        {
            return;
        }

        lock (_gate)
        {
            Note(snapshot, table.Over());
        }

        // The script is read by one answering at a time: a change that comes while an answer is on its way to
        // the match, before it is taken off the script, would read the answer of the question before.
        if (snapshot.Waiting is null)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _answering, 1, 0) == 0)
        {
            _ = Task.Run(() => AnswerAsync(table));
        }
        else
        {
            Volatile.Write(ref _again, 1);
        }
    }

    private void Note(SeatSnapshot snapshot, bool over)
    {
        var round = snapshot.Board.RoundNumber;
        if (snapshot.Waiting is { } waiting && round is { } asked)
        {
            var question = new SeatQuestion(waiting.Kind, waiting.Creature);
            for (var index = 0; index < _shown.Length; index++)
            {
                _shown[index] |= _lesson.Steps[index].ShowsAt(question, asked);
            }
        }

        _done |= over || snapshot.Board.State == MatchState.Ended || (_shown[^1] && round > _lesson.Steps[^1].Round);
    }

    /// <summary>The recorded answer the seat's question is owed, with the asking it answers; none for a question that is the person's.</summary>
    private (Application.Matches.Decisions.PlayerDecision Decision, long Asked)? Owed(SeatSnapshot snapshot) =>
        snapshot.Waiting is { } waiting && _assisted.Answer(new SeatView(snapshot.Board, snapshot.Options, new SeatQuestion(waiting.Kind, waiting.Creature))) is { } decision
            ? (decision, waiting.Asked)
            : null;

    /// <summary>
    /// Gives the recorded answers while the seat is asked assisted questions, one of the match's turns each. An
    /// answer the match did not take, because the person's own got there first, the table was let go of or it
    /// could not be written, hands every question back to the person: the script no longer says what the seat
    /// did, and a question nobody answers must be one the page draws.
    /// </summary>
    private async Task AnswerAsync(Seat table)
    {
        try
        {
            while (true)
            {
                // The seat is read before the gate: reading may move the session, which is not this play's to hold.
                var snapshot = table.Read();
                (Application.Matches.Decisions.PlayerDecision Decision, long Asked)? owed = null;
                if (snapshot is not null && HandsBack(() => owed = Owed(snapshot)))
                {
                    SayGaveBack();
                }

                if (owed is not { } answer)
                {
                    break;
                }

                var taken = (await table.Decide(answer.Decision, answer.Asked)).Taken;
                if (HandsBack(taken ? _assisted.Given : _assisted.HandBack))
                {
                    SayGaveBack();
                }
            }
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            // The table says what it could not do when its own step fails; the question is the person's to answer.
            Console.WriteLine($"  Tutorial {Order}: an assisted question could not be answered ({failure.GetType().Name}).");
            if (HandsBack(_assisted.HandBack))
            {
                SayGaveBack();
            }
        }
        finally
        {
            Volatile.Write(ref _answering, 0);
        }

        // A change that came while this was finishing found it busy: it is looked at now.
        if (Interlocked.Exchange(ref _again, 0) != 0)
        {
            Look();
        }
    }
}
