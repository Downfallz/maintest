using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A seat held by a person: every decision the match asks of it waits for a tap, which arrives on another
/// thread entirely.
/// </summary>
/// <remarks>
/// It blocks the driver on purpose. The driver asks a seat for a decision and expects one back; a person is
/// simply an agent that takes a while. Blocking is what lets the whole engine, its gates and ADR 0039's
/// per-creature board re-read work unchanged, and it is the reason a host built on this plays one session per
/// process (<c>docs/tabletop/playtest-app.md</c>). What it does not do is decide anything: the question it is
/// waiting on is public, and whoever answers has to answer that exact question.
/// </remarks>
internal sealed class HumanSeat(CancellationToken cancellation) : IPlayerAgent
{
    private readonly Lock _gate = new();
    private Question? _question;
    private TaskCompletionSource<PlayerDecision>? _answer;

    // Counts the questions this seat has been asked, so each one is told apart from the next of the same shape.
    private long _asked;

    /// <summary>What this seat is waiting for, or <c>null</c> when the match is not asking it anything.</summary>
    public Question? Waiting
    {
        get
        {
            lock (_gate)
            {
                return _question;
            }
        }
    }

    /// <summary>
    /// Answers the question the seat is waiting on. False when it is waiting for nothing, or for something
    /// else — a tap on a screen that has moved on is late, not illegal, and the host answers it as such.
    /// </summary>
    /// <param name="asked">
    /// Which asking the caller believes it is answering. Checked here, under the same lock that hands the
    /// answer over, because anywhere else it is a check and then a gap: two callers can both read the seat's
    /// current question, both agree it is theirs, and the first can advance the driver to the next question of
    /// the same shape while the second is still on its way. <see cref="Question.Answers" /> would then accept
    /// that second tap for a question nobody meant it for — a pick spent by accident, or a choice the round no
    /// longer allows handed to the driver.
    /// </param>
    public bool Submit(PlayerDecision decision, long asked)
    {
        ArgumentNullException.ThrowIfNull(decision);

        lock (_gate)
        {
            if (_question is not { } question || _answer is not { } answer || question.Asked != asked || !question.Answers(decision))
            {
                return false;
            }

            if (!answer.TrySetResult(decision))
            {
                return false;
            }

            // Stop waiting here rather than where the decision is picked up. The seat is answered the moment
            // this returns, but the thread it unblocks needs a scheduler before it can say so: a page polling
            // in that window would be shown the question it just answered, and a second tap on it would be
            // refused as late.
            _question = null;
            _answer = null;
            return true;
        }
    }

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        var decision = Ask(new Question(PlayerOptionsKind.Evolution, Creature: null));
        return decision.IsPass
            ? EvolutionDecision.Pass
            : EvolutionDecision.Unlock(new EvolutionChoice(Required(decision.Creature), Required(decision.Tier)));
    }

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) =>
        Required(Ask(new Question(PlayerOptionsKind.Speed, creature)).Speed);

    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) =>
        Ask(new Question(PlayerOptionsKind.TieOrder, Creature: null)).Order;

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
    {
        ArgumentNullException.ThrowIfNull(intentOption);
        return Required(Ask(new Question(PlayerOptionsKind.Intent, intentOption.Creature)).Spell);
    }

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Ask(new Question(PlayerOptionsKind.Target, options.Actor)).Targets;
    }

    private PlayerDecision Ask(Question shape)
    {
        // Stamped here rather than at the four call sites, which name the shape and have no reason to know
        // that two askings of it are two things.
        var question = shape with { Asked = Interlocked.Increment(ref _asked) };
        var answer = new TaskCompletionSource<PlayerDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _question = question;
            _answer = answer;
        }

        try
        {
            return answer.Task.WaitAsync(cancellation).GetAwaiter().GetResult();
        }
        finally
        {
            lock (_gate)
            {
                _question = null;
                _answer = null;
            }
        }
    }

    /// <summary>
    /// A decision that reached the seat without the field its kind needs is a bug in the host, not a late tap:
    /// the check refuses that shape before anything is handed over.
    /// </summary>
    private static TValue Required<TValue>(TValue? value)
        where TValue : struct =>
        value ?? throw new InvalidOperationException("The decision reached the seat without the value its kind needs.");

    private static TValue Required<TValue>(TValue? value)
        where TValue : class =>
        value ?? throw new InvalidOperationException("The decision reached the seat without the value its kind needs.");

    /// <summary>
    /// What the match is asking this seat. The creature is the one being asked about for a speed choice, an
    /// intent and a target binding, and none for evolution or a tie order, which are asked of the player rather
    /// than of one creature.
    /// </summary>
    /// <param name="Asked">
    /// Which asking this is, counted by the seat. It exists so that two questions of the same shape are two
    /// questions: a seat with two Evolution picks in a round is asked <c>(Evolution, null)</c> twice, and
    /// anything comparing askings by their shape alone -- the decision clock does -- would treat the second as
    /// a continuation of the first, keep the first's stamp for it and then take that stamp away when the first
    /// was answered. Nothing else reads it; it is identity, not information.
    /// </param>
    internal sealed record Question(PlayerOptionsKind Kind, CreatureId? Creature, long Asked = 0)
    {
        /// <summary>Whether a decision answers this question rather than the previous one.</summary>
        public bool Answers(PlayerDecision decision)
        {
            ArgumentNullException.ThrowIfNull(decision);
            if (decision.Kind != Kind)
            {
                return false;
            }

            // Evolution names its own creature; the other three are asked about one, and the answer is for it.
            return Kind == PlayerOptionsKind.Evolution || Creature is null || decision.Creature is null || decision.Creature == Creature;
        }
    }
}
