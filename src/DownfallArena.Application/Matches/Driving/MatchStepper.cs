using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// Plays a started match a step at a time through the public commands: every decision a Player agent owes is
/// taken, and the step stops where a person is asked or the match is over. Nothing waits in between, so a
/// caller holds a match only for the length of a step (ADR 0159).
/// </summary>
/// <remarks>
/// It plays the match the <see cref="MatchDriver" /> plays at a table (<c>AsksSpeedsAndIntentsTogether</c>):
/// the same seats asked the same things on the same boards, and the same commands in the same order.
/// <c>MatchStepperTests</c> holds the two to each other. What the driver keeps in a suspended call, the answers
/// two seats give at once, is here a value handed in and handed back (<see cref="HeldAnswers" />).
/// </remarks>
public sealed class MatchStepper(MatchCommandHandlers commands, MatchQueryHandlers queries)
{
    private static readonly PlayerSlot[] Slots = [PlayerSlot.Player1, PlayerSlot.Player2];

    /// <summary>
    /// Takes every decision the Player agents owe, until a person is asked or the match is over.
    /// </summary>
    public Task<Result<MatchStep>> AdvanceAsync(MatchId matchId, MatchSeats seats, HeldAnswers held, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seats);
        ArgumentNullException.ThrowIfNull(held);
        return RunAsync(matchId, seats, held, [], cancellationToken);
    }

    /// <summary>
    /// Takes the decision of a seat nobody here answers for, then advances. Refused, and nothing changed, when
    /// the seat is not being asked this (<see cref="DecisionErrors.NotPending" />: a tap on a screen that has
    /// moved on is late, not illegal) or when its options do not offer it.
    /// </summary>
    public async Task<Result<MatchStep>> SubmitAsync(MatchId matchId, MatchSeats seats, HeldAnswers held, SeatDecision submitted, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seats);
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(submitted);
        var (slot, decision) = submitted;
        if (seats.Of(slot) is not null)
        {
            throw new ArgumentException($"A Player agent sits at {slot} and answers for it.", nameof(submitted));
        }

        var read = await ReadAsync(matchId, cancellationToken);
        if (read.IsFailure)
        {
            return Result.Failure<MatchStep>(read.Error);
        }

        var table = read.Value;
        if (Question(table, slot, held) is not { } question || !question.Answers(decision))
        {
            return Result.Failure<MatchStep>(DecisionErrors.NotPending);
        }

        var options = table.Of(slot).Options;
        var offered = PlayerDecisionCheck.Validate(WithAnswers(options, held.Of(slot)), decision);
        if (offered.IsFailure)
        {
            return Result.Failure<MatchStep>(offered.Error);
        }

        if (table.AsksTogether)
        {
            held = held.With(slot, decision);
        }
        else
        {
            var applied = await ApplyAsync(matchId, slot, decision, options.Target, cancellationToken);
            if (applied.IsFailure)
            {
                return Result.Failure<MatchStep>(applied.Error);
            }
        }

        return await RunAsync(matchId, seats, held, [submitted], cancellationToken);
    }

    /// <summary>
    /// What a seat reads between two steps: its board and options with the answers it is holding, and the
    /// question it has to answer. Read once the match has been advanced: a Player agent that still owes a
    /// decision is not what this describes.
    /// </summary>
    public async Task<Result<SeatView>> SeatAsync(MatchId matchId, PlayerSlot slot, HeldAnswers held, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(held);
        var read = await ReadAsync(matchId, cancellationToken);
        if (read.IsFailure)
        {
            return Result.Failure<SeatView>(read.Error);
        }

        var seat = read.Value.Of(slot);
        var answers = read.Value.AsksTogether ? held.Of(slot) : [];
        return Result.Success(new SeatView(WithAnswers(seat.Board, answers), WithAnswers(seat.Options, answers), Question(read.Value, slot, held)));
    }

    private async Task<Result<MatchStep>> RunAsync(MatchId matchId, MatchSeats seats, HeldAnswers held, List<SeatDecision> decided, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await ReadAsync(matchId, cancellationToken);
            if (read.IsFailure)
            {
                return Result.Failure<MatchStep>(read.Error);
            }

            var table = read.Value;
            if (!table.InProgress)
            {
                // Answers held when the match ended under them -- a concession (ADR 0087) -- are nobody's any more.
                return table.One.Board.Outcome is { } outcome
                    ? Result.Success(new MatchStep(outcome, HeldAnswers.None, [], decided))
                    : Result.Failure<MatchStep>(MatchErrors.NotInProgress);
            }

            if (table.AsksTogether)
            {
                foreach (var slot in Slots)
                {
                    if (seats.Of(slot) is not { } agent)
                    {
                        continue;
                    }

                    foreach (var answer in Answers(agent, table.Of(slot), held.Of(slot)))
                    {
                        held = held.With(slot, answer);
                        decided.Add(new SeatDecision(slot, answer));
                    }
                }

                var asked = Slots.Where(slot => Question(table.Of(slot).Options, held.Of(slot)) is not null).ToList();
                if (asked.Count > 0)
                {
                    return Result.Success(new MatchStep(null, held, asked, decided));
                }

                await RevealAsync(matchId, held, cancellationToken);
                held = HeldAnswers.None;
                continue;
            }

            if (!held.IsEmpty)
            {
                throw new InvalidOperationException($"Match {matchId} holds answers while its seats are not being asked together.");
            }

            if (Turn(table) is not { } acting)
            {
                throw new InvalidOperationException($"Match {matchId} is in progress but no player can act.");
            }

            if (seats.Of(acting) is not { } bot)
            {
                return Result.Success(new MatchStep(null, held, [acting], decided));
            }

            var acted = await ActAsync(matchId, acting, bot, table.Of(acting), decided, cancellationToken);
            if (acted.IsFailure)
            {
                return Result.Failure<MatchStep>(acted.Error);
            }
        }
    }

    /// <summary>
    /// Hands the match the answers both seats were holding, seat 1's first and each seat's in the order it gave
    /// them: the order the driver submits them in, and so the order a record of them replays in.
    /// </summary>
    private async Task RevealAsync(MatchId matchId, HeldAnswers held, CancellationToken cancellationToken)
    {
        if (held.IsEmpty)
        {
            throw new InvalidOperationException($"Match {matchId} asks both seats at once and neither has anything to answer.");
        }

        foreach (var slot in Slots)
        {
            foreach (var answer in held.Of(slot))
            {
                Accept(await ApplyAsync(matchId, slot, answer, target: null, cancellationToken));
            }
        }
    }

    /// <summary>
    /// One seat's turn taken by its Player agent, as the driver takes it: one pick, every missing speed on the
    /// same board, a tie order, every intent on the board the one before it left (ADR 0039), or one action.
    /// </summary>
    private async Task<Result> ActAsync(MatchId matchId, PlayerSlot slot, IPlayerAgent agent, Seat seat, List<SeatDecision> decided, CancellationToken cancellationToken)
    {
        var (board, options) = seat;
        switch (options.Kind)
        {
            case PlayerOptionsKind.Evolution:
                await TakeAsync(Decision(agent.DecideEvolution(board, Section(options.Evolution))));
                break;
            case PlayerOptionsKind.Speed:
                foreach (var creature in Section(options.Speed).Missing)
                {
                    await TakeAsync(PlayerDecision.ChooseSpeed(creature, agent.DecideSpeed(board, creature)));
                }

                break;
            case PlayerOptionsKind.TieOrder:
                await TakeAsync(PlayerDecision.OrderTies(agent.DecideTieOrder(board, Section(options.TieOrder))));
                break;
            case PlayerOptionsKind.Intent:
                foreach (var option in Section(options.Intent).Creatures)
                {
                    var current = await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(matchId, slot), cancellationToken);
                    if (current.IsFailure)
                    {
                        return Result.Failure(current.Error);
                    }

                    await TakeAsync(PlayerDecision.DeclareIntent(option.Creature, agent.DecideIntent(current.Value, option)));
                }

                break;
            case PlayerOptionsKind.Target:
                await TakeAsync(PlayerDecision.BindTargets(agent.DecideTargets(board, Section(options.Target))));
                break;
            case PlayerOptionsKind.Action:
                var offered = Section(options.Action);
                var chosen = agent.DecideAction(board, offered);
                await TakeAsync(PlayerDecision.Act(offered.Actor, chosen.Spell, chosen.Targets));
                break;
            default:
                throw new InvalidOperationException($"{slot} has no decision of kind {options.Kind} to make.");
        }

        return Result.Success();

        async Task TakeAsync(PlayerDecision decision)
        {
            decided.Add(new SeatDecision(slot, decision));
            Accept(await ApplyAsync(matchId, slot, decision, options.Target, cancellationToken));
        }
    }

    /// <summary>
    /// What a Player agent answers when both seats are asked at once, after the answers the seat already holds:
    /// every pick of its opportunity up to its pass, a speed for every creature without one, an intent for every
    /// creature. Each pick and each intent is decided on the board its owner would read after the ones before it,
    /// built here because nothing is submitted until both seats are done (ADR 0089, ADR 0111, ADR 0117).
    /// </summary>
    private static List<PlayerDecision> Answers(IPlayerAgent agent, Seat seat, IReadOnlyList<PlayerDecision> held)
    {
        var (board, options) = seat;
        var answers = new List<PlayerDecision>();
        switch (options.Kind)
        {
            case PlayerOptionsKind.Evolution:
                var (picked, passed) = Picks(held);
                var left = Left(Section(options.Evolution), picked);
                while (!passed && left.RemainingPicks > 0)
                {
                    var decision = agent.DecideEvolution(board with { EvolutionChoices = [.. board.EvolutionChoices, .. picked] }, left);
                    answers.Add(Decision(decision));
                    if (decision.Choice is not { } choice)
                    {
                        break;
                    }

                    picked.Add(choice);
                    left = left.After(choice);
                }

                break;
            case PlayerOptionsKind.Speed:
                foreach (var creature in Section(options.Speed).Missing.Where(creature => !Holds(held, creature)))
                {
                    answers.Add(PlayerDecision.ChooseSpeed(creature, agent.DecideSpeed(board, creature)));
                }

                break;
            case PlayerOptionsKind.Intent:
                var declared = Intents(held);
                foreach (var option in Section(options.Intent).Creatures.Where(option => !Holds(held, option.Creature)))
                {
                    var spell = agent.DecideIntent(board with { Intents = [.. board.Intents, .. declared] }, option);
                    answers.Add(PlayerDecision.DeclareIntent(option.Creature, spell));
                    declared.Add(new CombatIntent(option.Creature, spell));
                }

                break;
            default:
                break;
        }

        return answers;
    }

    private Task<Result> ApplyAsync(MatchId matchId, PlayerSlot slot, PlayerDecision decision, TargetOptions? target, CancellationToken cancellationToken) =>
        decision.Kind switch
        {
            PlayerOptionsKind.Evolution when decision.IsPass =>
                commands.PassEvolution.HandleAsync(new PassEvolution(matchId, slot), cancellationToken),
            PlayerOptionsKind.Evolution =>
                commands.SubmitEvolutionChoice.HandleAsync(new SubmitEvolutionChoice(matchId, slot, Required(decision.Creature), Required(decision.Tier)), cancellationToken),
            PlayerOptionsKind.Speed =>
                commands.SubmitSpeedChoice.HandleAsync(new SubmitSpeedChoice(matchId, slot, Required(decision.Creature), Required(decision.Speed)), cancellationToken),
            PlayerOptionsKind.TieOrder =>
                commands.SubmitTieOrder.HandleAsync(new SubmitTieOrder(matchId, slot, decision.Order), cancellationToken),
            PlayerOptionsKind.Intent =>
                commands.SubmitIntent.HandleAsync(new SubmitIntent(matchId, slot, Required(decision.Creature), Required(decision.Spell)), cancellationToken),
            PlayerOptionsKind.Target =>
                commands.SubmitAction.HandleAsync(new SubmitAction(matchId, slot, Section(target).Actor, Section(target).Spell, decision.Targets), cancellationToken),
            PlayerOptionsKind.Action =>
                commands.SubmitAction.HandleAsync(new SubmitAction(matchId, slot, Required(decision.Creature), Required(decision.Spell), decision.Targets), cancellationToken),
            _ => throw new InvalidOperationException($"A decision of kind {decision.Kind} is not one a seat makes."),
        };

    private async Task<Result<Table>> ReadAsync(MatchId matchId, CancellationToken cancellationToken)
    {
        var seats = new List<Seat>(Slots.Length);
        foreach (var slot in Slots)
        {
            var board = await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(matchId, slot), cancellationToken);
            if (board.IsFailure)
            {
                return Result.Failure<Table>(board.Error);
            }

            var options = await queries.GetPlayerOptions.HandleAsync(new GetPlayerOptions(matchId, slot), cancellationToken);
            if (options.IsFailure)
            {
                return Result.Failure<Table>(options.Error);
            }

            seats.Add(new Seat(board.Value, options.Value));
        }

        return Result.Success(new Table(seats[0], seats[1]));
    }

    /// <summary>
    /// What a seat is being asked, or nothing while it waits: its next unanswered question when both seats are
    /// asked at once, and otherwise the whole turn of the first seat, in seat order, with a decision to make.
    /// </summary>
    private static SeatQuestion? Question(Table table, PlayerSlot slot, HeldAnswers held)
    {
        if (!table.InProgress)
        {
            return null;
        }

        if (table.AsksTogether)
        {
            return Question(table.Of(slot).Options, held.Of(slot));
        }

        return Turn(table) == slot ? Question(table.Of(slot).Options, []) : null;
    }

    private static SeatQuestion? Question(PlayerOptions options, IReadOnlyList<PlayerDecision> held)
    {
        switch (options.Kind)
        {
            case PlayerOptionsKind.Evolution:
                var (picked, passed) = Picks(held);
                return passed || Left(Section(options.Evolution), picked).RemainingPicks <= 0 ? null : new SeatQuestion(PlayerOptionsKind.Evolution, null);
            case PlayerOptionsKind.Speed:
                var missing = Section(options.Speed).Missing.Where(creature => !Holds(held, creature)).ToList();
                return missing.Count == 0 ? null : new SeatQuestion(PlayerOptionsKind.Speed, missing[0]);
            case PlayerOptionsKind.Intent:
                var undeclared = Section(options.Intent).Creatures.Where(option => !Holds(held, option.Creature)).ToList();
                return undeclared.Count == 0 ? null : new SeatQuestion(PlayerOptionsKind.Intent, undeclared[0].Creature);
            case PlayerOptionsKind.TieOrder:
                return new SeatQuestion(PlayerOptionsKind.TieOrder, null);
            case PlayerOptionsKind.Target:
                return new SeatQuestion(PlayerOptionsKind.Target, Section(options.Target).Actor);
            case PlayerOptionsKind.Action:
                return new SeatQuestion(PlayerOptionsKind.Action, Section(options.Action).Actor);
            default:
                return null;
        }
    }

    /// <summary>The first seat, in seat order, with a decision to make, or none.</summary>
    private static PlayerSlot? Turn(Table table)
    {
        foreach (var slot in Slots)
        {
            if (table.Of(slot).Options.Kind is not (PlayerOptionsKind.Waiting or PlayerOptionsKind.Ended))
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>
    /// A seat's board with the picks, speeds and intents it is holding. Until both seats are done the match has
    /// none of them, and they are the very thing a player chooses the next creature's on (ADR 0039).
    /// </summary>
    private static PlayerBoardState WithAnswers(PlayerBoardState board, IReadOnlyList<PlayerDecision> held)
    {
        if (held.Count == 0)
        {
            return board;
        }

        var (picked, passed) = Picks(held);
        var speeds = held
            .Where(answer => answer.Kind == PlayerOptionsKind.Speed)
            .Select(answer => new SpeedChoice(Required(answer.Creature), Required(answer.Speed)));
        return board with
        {
            EvolutionChoices = [.. board.EvolutionChoices, .. picked],
            HasPassedEvolution = board.HasPassedEvolution || passed,
            SpeedChoices = [.. board.SpeedChoices, .. speeds],
            Intents = [.. board.Intents, .. Intents(held)],
        };
    }

    /// <summary>
    /// A seat's options with the picks it is holding taken out: each takes a pick and its creature, and a seat
    /// that has passed or has no pick left is offered nothing more, so a second pick can neither be offered nor
    /// take the creature the first one went to (ADR 0117).
    /// </summary>
    private static PlayerOptions WithAnswers(PlayerOptions options, IReadOnlyList<PlayerDecision> held)
    {
        if (options.Evolution is not { } evolution || held.Count == 0)
        {
            return options;
        }

        var (picked, passed) = Picks(held);
        var left = Left(evolution, picked);
        return passed || left.RemainingPicks <= 0
            ? new PlayerOptions { Kind = PlayerOptionsKind.Waiting, SubPhase = options.SubPhase }
            : options with { Evolution = left };
    }

    private static (List<EvolutionChoice> Picked, bool Passed) Picks(IReadOnlyList<PlayerDecision> held)
    {
        var evolution = held.Where(answer => answer.Kind == PlayerOptionsKind.Evolution).ToList();
        List<EvolutionChoice> picked = [.. evolution.Where(answer => !answer.IsPass).Select(answer => new EvolutionChoice(Required(answer.Creature), Required(answer.Tier)))];
        return (picked, evolution.Exists(answer => answer.IsPass));
    }

    private static EvolutionOptions Left(EvolutionOptions options, IReadOnlyList<EvolutionChoice> picked) =>
        picked.Aggregate(options, (left, choice) => left.After(choice));

    private static List<CombatIntent> Intents(IReadOnlyList<PlayerDecision> held) =>
        [.. held.Where(answer => answer.Kind == PlayerOptionsKind.Intent).Select(answer => new CombatIntent(Required(answer.Creature), Required(answer.Spell)))];

    /// <summary>Whether a seat already holds its speed or its intent for a creature.</summary>
    private static bool Holds(IReadOnlyList<PlayerDecision> held, CreatureId creature) =>
        held.Any(answer => answer.Kind is PlayerOptionsKind.Speed or PlayerOptionsKind.Intent && answer.Creature == creature);

    private static PlayerDecision Decision(EvolutionDecision decision) =>
        decision.Choice is { } choice ? PlayerDecision.Buy(choice.Creature, choice.Tier) : PlayerDecision.Pass;

    private static TSection Section<TSection>(TSection? section)
        where TSection : class =>
        section ?? throw new InvalidOperationException($"The options announce a decision but carry no {typeof(TSection).Name}.");

    private static TValue Required<TValue>(TValue? value)
        where TValue : struct =>
        value ?? throw new InvalidOperationException("A decision came without the value its kind needs.");

    private static TValue Required<TValue>(TValue? value)
        where TValue : class =>
        value ?? throw new InvalidOperationException("A decision came without the value its kind needs.");

    /// <summary>
    /// A refused decision is a bug, in an agent or in the check a person's went through, with one exception: the
    /// match ended between the question and the answer (ADR 0087). The next read finds the outcome.
    /// </summary>
    private static void Accept(Result result)
    {
        if (result.IsFailure && result.Error != MatchErrors.NotInProgress)
        {
            throw new InvalidOperationException($"A decision was refused: {result.Error.Code} ({result.Error.Message}).");
        }
    }

    private sealed record Seat(PlayerBoardState Board, PlayerOptions Options);

    private sealed record Table(Seat One, Seat Two)
    {
        public bool InProgress => One.Board.State == MatchState.InProgress;

        /// <summary>
        /// Whether both seats are being asked the same thing and neither can see the other's answer: their
        /// picks, their speeds or their intents, each made face down (ADR 0092, ADR 0111).
        /// </summary>
        public bool AsksTogether =>
            InProgress && One.Options.Kind == Two.Options.Kind
            && One.Options.Kind is PlayerOptionsKind.Evolution or PlayerOptionsKind.Speed or PlayerOptionsKind.Intent;

        public Seat Of(PlayerSlot slot) => slot == PlayerSlot.Player1 ? One : Two;
    }
}
