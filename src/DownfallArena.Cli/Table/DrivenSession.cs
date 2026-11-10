using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A table's match played by the driver, blocked on its seats (<see cref="TableSession" />, <see cref="HumanSeat" />,
/// <see cref="SeatAgent" />): the model every table had before ADR 0159, and the one practice tables keep until
/// they move too. What is here is what the routes used to do to that model themselves.
/// </summary>
/// <param name="run">The recording, or none: what a swap is written down in, and what says the trace has grown.</param>
/// <param name="reads">The read side to ask instead of the session's own, or none: a test puts a slow or a moving one here.</param>
internal sealed class DrivenSession(TableSession session, IReadOnlyList<TableSeat> seats, PlaytestRun? run = null, MatchQueryHandlers? reads = null) : ITableSession
{
    private readonly MatchQueryHandlers _reads = reads ?? session.Queries;

    public MatchId MatchId => session.MatchId;

    public RuleSet Rules => session.Rules;

    public Task<Result<MatchOutcome>> Outcome => session.Outcome;

    public bool IsOver => session.IsOver;

    public bool HasBegun => session.HasBegun;

    public void Begin() => session.Begin();

    public void Dispose() => session.Dispose();

    public string SeatedName(PlayerSlot slot) => session.Seat(slot).Seated.Name;

    public bool PlayedByItsPerson(PlayerSlot slot) =>
        Person(slot) is { } person && ReferenceEquals(session.Seat(slot).Seated.Agent, person);

    public Swap? Pending(PlayerSlot slot) => session.Seat(slot).Pending;

    /// <remarks>
    /// Nothing about the seat is read before it is asked. The round it has reached, who is sitting in it and
    /// whether the swap is taken are one answer from one locked step inside the seat: read separately, a pending
    /// swap landing in between would name the wrong occupant as the one replaced, against a round the match had
    /// already left.
    /// </remarks>
    public Task<SwapOutcome> SwapAsync(PlayerSlot slot, Occupant next, string wanted, int round)
    {
        var outcome = session.Seat(slot).SwapAt(next, round);
        if (outcome.Taken)
        {
            // Written where it was asked, so a rebuilt table asks for it at the same point of the match.
            run?.Journal.SwapAsked(slot, wanted, round);
        }

        return Task.FromResult(outcome);
    }

    public async Task<Result<PlayerBoardState>> BoardAsync(PlayerSlot slot) =>
        await _reads.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, slot));

    /// <summary>
    /// The question the seat is blocked on, with the board and the options that go with it, read as one.
    /// </summary>
    /// <remarks>
    /// The question is what the seat is asked rather than what the sub-phase allows: a person acts when their
    /// own seat is asked, and the two differ while the other seat is still deciding. The three are read in
    /// separate steps and the match does not wait for a poll in between. The other seat resolves its action
    /// the moment it binds it (ADR 0083), so the match can reach this seat's next question during the reads,
    /// and a second device on the same seat can answer the question during them. Either way a question would
    /// be served beside options that are not its own. So the question is read again once the options are in,
    /// and the reads are repeated until it has not moved: it can only move on a decision, so this ends.
    /// </remarks>
    public async Task<Result<SeatSnapshot>> SnapshotAsync(PlayerSlot slot)
    {
        var person = Person(slot);
        while (true)
        {
            var waiting = person?.Waiting;
            var board = await BoardAsync(slot);
            if (board.IsFailure)
            {
                return Result.Failure<SeatSnapshot>(board.Error);
            }

            var options = await _reads.GetPlayerOptions.HandleAsync(new GetPlayerOptions(session.MatchId, slot));
            if (options.IsFailure)
            {
                return Result.Failure<SeatSnapshot>(options.Error);
            }

            if (person?.Waiting?.Asked != waiting?.Asked)
            {
                continue;
            }

            // How many cards the other side has face down, and nothing else about them. At a table that number
            // is public -- face-down cards are countable -- so it is served, as a count computed here rather
            // than as a list the client is trusted not to read (ADR 0054).
            var opponent = await BoardAsync(Other(slot));
            return Result.Success(new SeatSnapshot(
                waiting,
                WithAnswers(board.Value, person),
                WithAnswers(options.Value, board.Value, person),
                opponent.IsSuccess ? StillFaceDown(WithAnswers(opponent.Value, Person(Other(slot)))) : 0));
        }
    }

    /// <remarks>
    /// Checked against the options, and still refused when the seat moved on between the two: that is the race
    /// the driver would have thrown on, answered as the late tap it is. The asking is checked inside the seat,
    /// under the lock that hands the answer over.
    /// </remarks>
    public Task<DecisionTaken> DecideAsync(PlayerSlot slot, PlayerDecision decision, long asked, PlayerOptions offered, int? round) =>
        Task.FromResult(Person(slot) is { } person && person.Submit(decision, asked)
            ? new DecisionTaken(true, SetsOff(slot, person, decision, offered, round))
            : DecisionTaken.Late);

    /// <remarks>
    /// The command ends the match behind the driver's lock; every seat a person holds is then released, because
    /// the driver is blocked inside one of them waiting for a tap that is not coming, and it reads the outcome
    /// once it is let go. A table given up while it waited for its players begins now, so the driver reads the
    /// end and the session closes; it would otherwise wait for a player who is never coming to a match that is over.
    /// </remarks>
    public async Task<Result> ConcedeAsync(PlayerSlot slot)
    {
        var conceded = await session.Concede.HandleAsync(new Concede(session.MatchId, slot));
        if (conceded.IsFailure)
        {
            return conceded;
        }

        foreach (var seat in seats)
        {
            (seat.Person as HumanSeat)?.Release();
        }

        session.Begin();
        return conceded;
    }

    /// <summary>
    /// Waits until the engine has finished the command this decision caused, so what is checkpointed next is
    /// the whole of it and not the first event of it.
    /// </summary>
    /// <remarks>
    /// Two things in sequence, and neither is enough alone. The trace growing says the command has *started*:
    /// submitting only queues the driver, so until then this thread is ahead of it. Then a read through the
    /// table's gate says it has *finished* -- the driver holds that gate for a whole command, saving and
    /// dispatching inside it (<c>MatchWorkflow.CommitAsync</c>), so a read that comes back is that command
    /// having let go. One command raises several events, and watching the trace alone would stop at the first
    /// of them and checkpoint while the rest were still being dispatched. The gate is the completion signal
    /// the host would otherwise have had to invent, and it was already there.
    /// </remarks>
    public async Task SettledAsync(PlayerSlot slot, int traced)
    {
        if (run is { } recording)
        {
            await recording.WaitForTraceAsync(session.MatchId, traced, CancellationToken.None);
        }

        await BoardAsync(slot);
    }

    private HumanSeat? Person(PlayerSlot slot) => seats.FirstOrDefault(seat => seat.Slot == slot)?.Person as HumanSeat;

    private static PlayerSlot Other(PlayerSlot slot) => slot == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;

    /// <summary>
    /// How many of a board's intents are still face down. A round keeps every intent it was given
    /// (<c>Round.IntentsOf</c>) and tracks what has been turned over separately, in the actions the activation
    /// cursor has reached, so the count of the one is not the count of the other from the first reveal
    /// onwards. Subtracting here is what keeps the number and the reveal strip from contradicting each other
    /// on the same screen: three face down beside two of them face up is a table nobody would believe.
    /// </summary>
    private static int StillFaceDown(PlayerBoardState board)
    {
        var revealed = board.RevealedActions.Select(action => action.Actor).ToHashSet();
        return board.Intents.Count(intent => !revealed.Contains(intent.Actor));
    }

    /// <summary>
    /// A seat's board with the picks, speeds and intents its person has answered and the match does not have yet.
    /// Both seats are asked theirs at once and the answers reach the match together once both are done (ADR 0111,
    /// ADR 0117); until then the board would show a player none of what they have just chosen, which is the very
    /// thing they choose the next creature's on (ADR 0039). Once the match has an answer, its own copy is the one shown.
    /// </summary>
    private static PlayerBoardState WithAnswers(PlayerBoardState board, HumanSeat? person)
    {
        var given = person?.Given(board.RoundNumber) ?? [];
        if (given.Count == 0)
        {
            return board;
        }

        var picks = Unsent(board, given);
        var speeds = given
            .Where(answer => answer.Kind == PlayerOptionsKind.Speed && !board.SpeedChoices.Any(choice => choice.Creature == answer.Creature))
            .Select(answer => new SpeedChoice(answer.Creature!.Value, answer.Speed!.Value));
        var intents = given
            .Where(answer => answer.Kind == PlayerOptionsKind.Intent && !board.Intents.Any(intent => intent.Actor == answer.Creature))
            .Select(answer => new CombatIntent(answer.Creature!.Value, answer.Spell!));
        return board with
        {
            EvolutionChoices = [.. board.EvolutionChoices, .. picks.Choices],
            HasPassedEvolution = board.HasPassedEvolution || picks.Passed,
            SpeedChoices = [.. board.SpeedChoices, .. speeds],
            Intents = [.. board.Intents, .. intents],
        };
    }

    /// <summary>
    /// The options a seat is offered once the picks its person has answered and the match does not have yet are
    /// taken out of them: each takes a pick and its creature (<see cref="EvolutionOptions.After" />), and a seat that
    /// has passed or has no pick left is asked nothing more. Served to the page and checked against an answer, so a
    /// second pick can neither be offered nor take the creature the first one went to (ADR 0117).
    /// </summary>
    private static PlayerOptions WithAnswers(PlayerOptions options, PlayerBoardState board, HumanSeat? person)
    {
        if (options.Evolution is not { } evolution)
        {
            return options;
        }

        var picks = Unsent(board, person?.Given(board.RoundNumber) ?? []);
        foreach (var choice in picks.Choices)
        {
            evolution = evolution.After(choice);
        }

        return picks.Passed || evolution.RemainingPicks <= 0
            ? new PlayerOptions { Kind = PlayerOptionsKind.Waiting, SubPhase = options.SubPhase }
            : options with { Evolution = evolution };
    }

    /// <summary>The picks and the pass a person has answered that the match's board does not carry yet.</summary>
    private static (IReadOnlyList<EvolutionChoice> Choices, bool Passed) Unsent(PlayerBoardState board, IReadOnlyList<PlayerDecision> given)
    {
        var evolution = given.Where(answer => answer.Kind == PlayerOptionsKind.Evolution).ToList();
        var choices = evolution
            .Where(answer => answer is { IsPass: false, Creature: { } creature } && !board.EvolutionChoices.Any(choice => choice.Creature == creature))
            .Select(answer => new EvolutionChoice(answer.Creature!.Value, answer.Tier!))
            .ToList();
        return (choices, !board.HasPassedEvolution && evolution.Any(answer => answer.IsPass));
    }

    /// <summary>
    /// Whether an answer can have set a command off, which is when the trace is worth waiting for and keeping.
    /// One of a seat's picks, speeds or intents does not until its last, and the last does not while the other
    /// person is still being asked theirs: the answers reach the match together (ADR 0111, ADR 0117). Waiting for a
    /// trace that cannot grow costs the player its whole bound, a quarter of a second a tap with the buttons held,
    /// and the checkpoint it writes is the one already written. A seat's last pick is its pass or the one that
    /// spends the picks it was offered, which are the options the answer was checked against.
    /// </summary>
    private bool SetsOff(PlayerSlot slot, HumanSeat person, PlayerDecision decision, PlayerOptions options, int? round)
    {
        var last = decision.Kind switch
        {
            PlayerOptionsKind.Evolution => decision.IsPass || options.Evolution?.RemainingPicks <= 1,
            PlayerOptionsKind.Speed => person.Given(round).Count(given => given.Kind == decision.Kind) >= (options.Speed?.Missing.Count ?? 0),
            PlayerOptionsKind.Intent => person.Given(round).Count(given => given.Kind == decision.Kind) >= (options.Intent?.Creatures.Count ?? 0),
            _ => (bool?)null,
        };
        return last is not { } settles || (settles && Person(Other(slot))?.Waiting?.Kind != decision.Kind);
    }
}
