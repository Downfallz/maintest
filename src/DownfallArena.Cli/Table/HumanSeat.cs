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
    public bool Submit(PlayerDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        lock (_gate)
        {
            if (_question is not { } question || _answer is not { } answer || !question.Answers(decision))
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
            : EvolutionDecision.Unlock(new EvolutionChoice(Required(decision.Creature), Required(decision.Spell)));
    }

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) =>
        Required(Ask(new Question(PlayerOptionsKind.Speed, creature)).Speed);

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

    private PlayerDecision Ask(Question question)
    {
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
    /// intent and a target binding, and none for evolution, which is asked of the player rather than of one
    /// creature.
    /// </summary>
    internal sealed record Question(PlayerOptionsKind Kind, CreatureId? Creature)
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
