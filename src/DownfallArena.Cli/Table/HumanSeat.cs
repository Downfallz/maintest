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
/// per-creature board re-read work unchanged, and it is the reason a host built on this plays only so many
/// sessions at once (<c>docs/tabletop/playtest-app.md</c>, ADR 0081). What it does not do is decide anything: the question it is
/// waiting on is public, and whoever answers has to answer that exact question.
/// </remarks>
internal sealed class HumanSeat(CancellationToken cancellation) : IPlayerAgent
{
    private readonly Lock _gate = new();
    private Question? _question;
    private TaskCompletionSource<PlayerDecision?>? _answer;
    private bool _released;

    // Counts the questions this seat has been asked, so each one is told apart from the next of the same shape.
    private long _asked;

    // The round the question being asked belongs to, and the speeds and intents answered in it.
    private int? _round;
    private readonly List<(int? Round, PlayerDecision Decision)> _given = [];

    /// <summary>
    /// Whether the person has reached their seat: a request carrying its token has arrived (ADR 0092). The
    /// match begins once every person has, which is what a table waiting for its players waits for.
    /// </summary>
    public bool HasArrived => Volatile.Read(ref _arrived) != 0;

    private int _arrived;

    public void Arrive() => Volatile.Write(ref _arrived, 1);

    public void Depart() => Volatile.Write(ref _arrived, 0);

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

            if (decision.Kind is PlayerOptionsKind.Speed or PlayerOptionsKind.Intent)
            {
                _given.Add((_round, decision));
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

    /// <summary>
    /// The speeds and intents this seat has answered in a round, whether or not the match has them yet. Both seats
    /// are asked theirs at once and the answers are handed to the match only when both are done (ADR 0111), so
    /// until then the board does not show a player what they have just chosen; this is what the host adds to it.
    /// Answers of earlier rounds are forgotten when the seat is asked about a later one, never by a reader: a poll
    /// that read its board before the round moved on must not take the new round's answers with it.
    /// </summary>
    public IReadOnlyList<PlayerDecision> Given(int? round)
    {
        lock (_gate)
        {
            return [.. _given.Where(given => given.Round == round).Select(given => given.Decision)];
        }
    }

    /// <summary>
    /// Stops the seat waiting, now and for the rest of the match: the match has ended under it -- a concession
    /// (ADR 0087) -- and the question it is blocking the driver on has no answer. What the driver is handed
    /// from then on is a decision to a match that is over, which the match refuses and the driver reads as
    /// the end (<c>MatchDriver.Accept</c>); nothing in it is anybody's choice.
    /// </summary>
    public void Release()
    {
        lock (_gate)
        {
            _released = true;
            _answer?.TrySetResult(null);
            _question = null;
            _answer = null;
        }
    }

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options)
    {
        var decision = Ask(new Question(PlayerOptionsKind.Evolution, Creature: null));
        return decision is null || decision.IsPass
            ? EvolutionDecision.Pass
            : EvolutionDecision.Unlock(new EvolutionChoice(Required(decision.Creature), Required(decision.Tier)));
    }

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) =>
        Ask(new Question(PlayerOptionsKind.Speed, creature), board?.RoundNumber) is { } decision ? Required(decision.Speed) : Speed.Standard;

    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) =>
        Ask(new Question(PlayerOptionsKind.TieOrder, Creature: null))?.Order ?? [];

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption)
    {
        ArgumentNullException.ThrowIfNull(intentOption);
        if (Ask(new Question(PlayerOptionsKind.Intent, intentOption.Creature), board?.RoundNumber) is { } decision)
        {
            return Required(decision.Spell);
        }

        // Released: the match has ended and refuses the intent before it reads the spell, so any name will do,
        // and a creature can be asked with nothing it can cast.
        return intentOption.CastableSpells.Count > 0 ? intentOption.CastableSpells[0] : Nothing;
    }

    private static readonly SpellId Nothing = SpellId.Parse("spell:nothing:v1");

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Released: the ended match refuses whatever is handed to it, but a recording seat checks the answer
        // against the options before the match sees it, and refuses an answer they never offered as a bug. So a
        // released seat hands back the first set the options offer, which is nobody's choice either.
        return Ask(new Question(PlayerOptionsKind.Target, options.Actor))?.Targets ?? TargetSets.Of(options.LegalTargets).FirstOrDefault() ?? [];
    }

    /// <summary>
    /// A spell and its targets chosen together when the creature's slot comes up (ADR 0110): one question, about
    /// the creature at the slot, answered by one tap.
    /// </summary>
    public ChosenAction DecideAction(PlayerBoardState board, ActionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (Ask(new Question(PlayerOptionsKind.Action, options.Actor)) is { } decision)
        {
            return new ChosenAction(Required(decision.Spell), decision.Targets);
        }

        // Released: as for targets, the first pair the options offer, which the recording seat accepts and the
        // ended match refuses.
        return TargetSets.Pairs(options).FirstOrDefault() ?? new ChosenAction(Nothing, []);
    }

    /// <summary>The person's answer, or <c>null</c> from a seat that has been released: nobody is answering.</summary>
    private PlayerDecision? Ask(Question shape, int? round = null)
    {
        // Stamped here rather than at the call sites, which name the shape and have no reason to know
        // that two askings of it are two things.
        var question = shape with { Asked = Interlocked.Increment(ref _asked) };
        var answer = new TaskCompletionSource<PlayerDecision?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_released)
            {
                return null;
            }

            _question = question;
            _answer = answer;
            _round = round;
            if (round is not null)
            {
                _given.RemoveAll(given => given.Round != round);
            }
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
    /// intent, a target binding and an action chosen at the slot, and none for evolution or a tie order, which
    /// are asked of the player rather than of one creature.
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

            // Evolution names its own creature and a tie order names several; the other four are asked about one
            // creature, and the answer is for it.
            return Kind is PlayerOptionsKind.Evolution or PlayerOptionsKind.TieOrder || Creature is null || decision.Creature is null || decision.Creature == Creature;
        }
    }
}
