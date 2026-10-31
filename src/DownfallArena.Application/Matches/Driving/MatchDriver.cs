using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// Plays a started match to its end through the public commands: for each player in turn, asks what they
/// can do, lets their agent decide, and submits; the match resolves each action as its targets are confirmed
/// (ADR 0083). An agent that produces a refused
/// decision is a bug, reported as an invariant violation.
/// </summary>
public sealed class MatchDriver(MatchCommandHandlers commands, MatchQueryHandlers queries)
{
    /// <summary>
    /// Whether both seats are asked their speeds and their intents at once, as they are asked their picks (ADR
    /// 0111), and each seat all of its picks before either seat's reach the match (ADR 0117). A table sets it,
    /// where two people would otherwise wait on each other; bots answer at once, so a simulation leaves it off
    /// and keeps the order of its events and of its recorded steps fixed by the seed.
    /// </summary>
    public bool AsksSpeedsAndIntentsTogether { get; init; }

    public async Task<Result<MatchOutcome>> PlayAsync(MatchId matchId, IPlayerAgent player1, IPlayerAgent player2, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player1);
        ArgumentNullException.ThrowIfNull(player2);

        while (true)
        {
            var progressed = false;
            if (await AskTogetherAsync(matchId, player1, player2, cancellationToken))
            {
                continue;
            }

            foreach (var (slot, agent) in new[] { (PlayerSlot.Player1, player1), (PlayerSlot.Player2, player2) })
            {
                var board = await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(matchId, slot), cancellationToken);
                if (board.IsFailure)
                {
                    return Result.Failure<MatchOutcome>(board.Error);
                }

                if (board.Value.State != MatchState.InProgress)
                {
                    return board.Value.Outcome is { } outcome
                        ? Result.Success(outcome)
                        : Result.Failure<MatchOutcome>(MatchErrors.NotInProgress);
                }

                var options = await queries.GetPlayerOptions.HandleAsync(new GetPlayerOptions(matchId, slot), cancellationToken);
                if (options.IsFailure)
                {
                    return Result.Failure<MatchOutcome>(options.Error);
                }

                if (await ActAsync(matchId, slot, agent, board.Value, options.Value, cancellationToken))
                {
                    progressed = true;

                    // Asking speeds and intents at once, one answer and then the top of the loop again: an answer
                    // can open a question both seats are asked at once -- a tie order taken, the intents come
                    // next -- and the second seat read in this pass would be asked it alone (ADR 0111).
                    if (AsksSpeedsAndIntentsTogether)
                    {
                        break;
                    }
                }
            }

            if (!progressed)
            {
                throw new InvalidOperationException($"Match {matchId} is in progress but no player can act.");
            }
        }
    }

    /// <summary>
    /// Both players asked at once, when both are being asked the same thing and neither can see the other's
    /// answer: their package picks (ADR 0092), and at a table their speeds and their intents (ADR 0111) and every
    /// pick of their opportunity rather than one pick each at a time (ADR 0117). Each is
    /// made face down and revealed together, so the two questions are independent and asking them in turn only
    /// made the second player wait for the first. Each seat answers all of its own on its own thread, then the
    /// answers are submitted in seat order, so a rebuilt table replays the commands it recorded in the order it
    /// recorded them. False when the match is not at such a moment, and the loop below plays the seats in turn.
    /// </summary>
    private async Task<bool> AskTogetherAsync(MatchId matchId, IPlayerAgent player1, IPlayerAgent player2, CancellationToken cancellationToken)
    {
        var first = await AskingAsync(matchId, PlayerSlot.Player1, cancellationToken);
        var second = await AskingAsync(matchId, PlayerSlot.Player2, cancellationToken);
        if (first is not { } one || second is not { } two || one.Options.Kind != two.Options.Kind)
        {
            return false;
        }

        if (one.Options.Kind != PlayerOptionsKind.Evolution && !AsksSpeedsAndIntentsTogether)
        {
            return false;
        }

        switch (one.Options.Kind)
        {
            case PlayerOptionsKind.Evolution when AsksSpeedsAndIntentsTogether:
                var opportunities = await Task.WhenAll(
                    Task.Run(() => Picks(player1, one.Board, Section(one.Options.Evolution)), cancellationToken),
                    Task.Run(() => Picks(player2, two.Board, Section(two.Options.Evolution)), cancellationToken));
                foreach (var pick in opportunities[0])
                {
                    await SubmitEvolutionAsync(matchId, PlayerSlot.Player1, pick, cancellationToken);
                }

                foreach (var pick in opportunities[1])
                {
                    await SubmitEvolutionAsync(matchId, PlayerSlot.Player2, pick, cancellationToken);
                }

                return true;
            case PlayerOptionsKind.Evolution:
                var picks = await Task.WhenAll(
                    Task.Run(() => player1.DecideEvolution(one.Board, Section(one.Options.Evolution)), cancellationToken),
                    Task.Run(() => player2.DecideEvolution(two.Board, Section(two.Options.Evolution)), cancellationToken));
                await SubmitEvolutionAsync(matchId, PlayerSlot.Player1, picks[0], cancellationToken);
                await SubmitEvolutionAsync(matchId, PlayerSlot.Player2, picks[1], cancellationToken);
                return true;
            case PlayerOptionsKind.Speed:
                var speeds = await Task.WhenAll(
                    Task.Run(() => Speeds(player1, one.Board, Section(one.Options.Speed)), cancellationToken),
                    Task.Run(() => Speeds(player2, two.Board, Section(two.Options.Speed)), cancellationToken));
                await SubmitSpeedsAsync(matchId, PlayerSlot.Player1, speeds[0], cancellationToken);
                await SubmitSpeedsAsync(matchId, PlayerSlot.Player2, speeds[1], cancellationToken);
                return true;
            case PlayerOptionsKind.Intent:
                var intents = await Task.WhenAll(
                    Task.Run(() => Intents(player1, one.Board, Section(one.Options.Intent)), cancellationToken),
                    Task.Run(() => Intents(player2, two.Board, Section(two.Options.Intent)), cancellationToken));
                await SubmitIntentsAsync(matchId, PlayerSlot.Player1, intents[0], cancellationToken);
                await SubmitIntentsAsync(matchId, PlayerSlot.Player2, intents[1], cancellationToken);
                return true;
            default:
                return false;
        }
    }

    /// <summary>A seat's board and options, when it is being asked something; null otherwise.</summary>
    private async Task<(PlayerBoardState Board, PlayerOptions Options)?> AskingAsync(MatchId matchId, PlayerSlot slot, CancellationToken cancellationToken)
    {
        var board = await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(matchId, slot), cancellationToken);
        if (board.IsFailure || board.Value.State != MatchState.InProgress)
        {
            return null;
        }

        var options = await queries.GetPlayerOptions.HandleAsync(new GetPlayerOptions(matchId, slot), cancellationToken);
        return options.IsSuccess ? (board.Value, options.Value) : null;
    }

    /// <summary>
    /// Every pick of a seat's opportunity, up to its pass, each made on the board and with the options its owner
    /// would read after the picks before it. Nothing is submitted until both seats are done, so the driver builds
    /// them: a pick changes its owner's board by that pick and its options by one pick and that creature, and the
    /// other seat's neither (ADR 0089, ADR 0117; MatchDriverTests holds them to the match's).
    /// </summary>
    private static List<EvolutionDecision> Picks(IPlayerAgent agent, PlayerBoardState board, EvolutionOptions options)
    {
        var decisions = new List<EvolutionDecision>();
        var picked = new List<EvolutionChoice>();
        while (options.RemainingPicks > 0)
        {
            var decision = agent.DecideEvolution(board with { EvolutionChoices = [.. board.EvolutionChoices, .. picked] }, options);
            decisions.Add(decision);
            if (decision.Choice is not { } choice)
            {
                break;
            }

            picked.Add(choice);
            options = options.After(choice);
        }

        return decisions;
    }

    /// <summary>A speed for every creature still without one, each read on the same board, as the seat-by-seat loop reads them.</summary>
    private static List<(CreatureId Creature, Speed Speed)> Speeds(IPlayerAgent agent, PlayerBoardState board, SpeedOptions options) =>
        [.. options.Missing.Select(creature => (creature, agent.DecideSpeed(board, creature)))];

    /// <summary>
    /// An intent for every creature, each declared on the board its owner would read after declaring the ones
    /// before it (ADR 0039). Nothing is submitted until both seats are done, so that board is the one the match
    /// would serve, built here: an intent changes nothing on its owner's board but the owner's own intents, and
    /// nothing at all on the other's (MatchDriverTests holds the two to each other).
    /// </summary>
    private static List<CombatIntent> Intents(IPlayerAgent agent, PlayerBoardState board, IntentOptions options)
    {
        var declared = new List<CombatIntent>();
        foreach (var option in options.Creatures)
        {
            var current = board with { Intents = [.. board.Intents, .. declared] };
            declared.Add(new CombatIntent(option.Creature, agent.DecideIntent(current, option)));
        }

        return declared;
    }

    private async Task SubmitSpeedsAsync(MatchId matchId, PlayerSlot slot, List<(CreatureId Creature, Speed Speed)> speeds, CancellationToken cancellationToken)
    {
        foreach (var (creature, speed) in speeds)
        {
            Accept(await commands.SubmitSpeedChoice.HandleAsync(new SubmitSpeedChoice(matchId, slot, creature, speed), cancellationToken));
        }
    }

    private async Task SubmitIntentsAsync(MatchId matchId, PlayerSlot slot, List<CombatIntent> intents, CancellationToken cancellationToken)
    {
        foreach (var intent in intents)
        {
            Accept(await commands.SubmitIntent.HandleAsync(new SubmitIntent(matchId, slot, intent.Actor, intent.Spell), cancellationToken));
        }
    }

    private async Task<bool> ActAsync(MatchId matchId, PlayerSlot slot, IPlayerAgent agent, PlayerBoardState board, PlayerOptions options, CancellationToken cancellationToken)
    {
        switch (options.Kind)
        {
            case PlayerOptionsKind.Evolution:
                await EvolveAsync(matchId, slot, agent, board, Section(options.Evolution), cancellationToken);
                return true;
            case PlayerOptionsKind.Speed:
                foreach (var creature in Section(options.Speed).Missing)
                {
                    Accept(await commands.SubmitSpeedChoice.HandleAsync(new SubmitSpeedChoice(matchId, slot, creature, agent.DecideSpeed(board, creature)), cancellationToken));
                }

                return true;
            case PlayerOptionsKind.TieOrder:
                Accept(await commands.SubmitTieOrder.HandleAsync(new SubmitTieOrder(matchId, slot, agent.DecideTieOrder(board, Section(options.TieOrder))), cancellationToken));
                return true;
            case PlayerOptionsKind.Intent:
                // Re-read the board for each creature, because the previous one's intent is on it. A player
                // declares in sequence and knows what they have already declared; the projection has carried
                // those intents all along (`PlayerBoardState.Intents`) and this loop used to hand every
                // creature the same board from before the first of them chose (ADR 0039).
                foreach (var option in Section(options.Intent).Creatures)
                {
                    var current = await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(matchId, slot), cancellationToken);
                    if (current.IsFailure)
                    {
                        return false;
                    }

                    Accept(await commands.SubmitIntent.HandleAsync(new SubmitIntent(matchId, slot, option.Creature, agent.DecideIntent(current.Value, option)), cancellationToken));
                }

                return true;
            case PlayerOptionsKind.Target:
                await TargetAsync(matchId, slot, agent, board, Section(options.Target), cancellationToken);
                return true;
            case PlayerOptionsKind.Action:
                var offered = Section(options.Action);
                var chosen = agent.DecideAction(board, offered);
                Accept(await commands.SubmitAction.HandleAsync(new SubmitAction(matchId, slot, offered.Actor, chosen.Spell, chosen.Targets), cancellationToken));
                return true;
            default:
                return false;
        }
    }

    private Task EvolveAsync(MatchId matchId, PlayerSlot slot, IPlayerAgent agent, PlayerBoardState board, EvolutionOptions options, CancellationToken cancellationToken) =>
        SubmitEvolutionAsync(matchId, slot, agent.DecideEvolution(board, options), cancellationToken);

    private async Task SubmitEvolutionAsync(MatchId matchId, PlayerSlot slot, EvolutionDecision decision, CancellationToken cancellationToken)
    {
        if (decision.Choice is { } choice)
        {
            Accept(await commands.SubmitEvolutionChoice.HandleAsync(new SubmitEvolutionChoice(matchId, slot, choice.Creature, choice.Tier), cancellationToken));
        }
        else
        {
            Accept(await commands.PassEvolution.HandleAsync(new PassEvolution(matchId, slot), cancellationToken));
        }
    }

    private async Task TargetAsync(MatchId matchId, PlayerSlot slot, IPlayerAgent agent, PlayerBoardState board, TargetOptions options, CancellationToken cancellationToken)
    {
        var targets = agent.DecideTargets(board, options);
        Accept(await commands.SubmitAction.HandleAsync(new SubmitAction(matchId, slot, options.Actor, options.Spell, targets), cancellationToken));
    }

    private static TSection Section<TSection>(TSection? section)
        where TSection : class =>
        section ?? throw new InvalidOperationException($"The options announce a decision but carry no {typeof(TSection).Name}.");

    /// <summary>
    /// A refused decision is a bug in an agent, with one exception: the match ended between the question and
    /// the answer. A concession lands while a seat is being asked (ADR 0087), so the answer that seat gives is
    /// to a match that is over and is refused as such; the loop reads the outcome next.
    /// </summary>
    private static void Accept(Result result)
    {
        if (result.IsFailure && result.Error != MatchErrors.NotInProgress)
        {
            throw new InvalidOperationException($"An agent's decision was refused: {result.Error.Code} ({result.Error.Message}).");
        }
    }
}
