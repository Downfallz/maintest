using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Domain.Matches;
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
    public async Task<Result<MatchOutcome>> PlayAsync(MatchId matchId, IPlayerAgent player1, IPlayerAgent player2, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player1);
        ArgumentNullException.ThrowIfNull(player2);

        while (true)
        {
            var progressed = false;
            if (await EvolveTogetherAsync(matchId, player1, player2, cancellationToken))
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

                progressed |= await ActAsync(matchId, slot, agent, board.Value, options.Value, cancellationToken);
            }

            if (!progressed)
            {
                throw new InvalidOperationException($"Match {matchId} is in progress but no player can act.");
            }
        }
    }

    /// <summary>
    /// Both players' package picks, asked at once when both are being asked (ADR 0092). The picks are made face
    /// down and revealed together (ADR 0089), so neither board shows the other's choice and the two questions
    /// are independent: asking them in turn only made the second player wait for the first. The answers are
    /// still submitted in seat order, so the engine sees exactly the commands it saw before. False when the
    /// match is not at that moment, and the loop below plays the seats in turn as it always did.
    /// </summary>
    private async Task<bool> EvolveTogetherAsync(MatchId matchId, IPlayerAgent player1, IPlayerAgent player2, CancellationToken cancellationToken)
    {
        var first = await AskingAsync(matchId, PlayerSlot.Player1, cancellationToken);
        var second = await AskingAsync(matchId, PlayerSlot.Player2, cancellationToken);
        if (first is not { } one || second is not { } two)
        {
            return false;
        }

        var decisions = await Task.WhenAll(
            Task.Run(() => player1.DecideEvolution(one.Board, one.Options), cancellationToken),
            Task.Run(() => player2.DecideEvolution(two.Board, two.Options), cancellationToken));
        await SubmitEvolutionAsync(matchId, PlayerSlot.Player1, decisions[0], cancellationToken);
        await SubmitEvolutionAsync(matchId, PlayerSlot.Player2, decisions[1], cancellationToken);
        return true;
    }

    /// <summary>A seat's board and its evolution options, when a package pick is what it is being asked for; null otherwise.</summary>
    private async Task<(PlayerBoardState Board, EvolutionOptions Options)?> AskingAsync(MatchId matchId, PlayerSlot slot, CancellationToken cancellationToken)
    {
        var board = await queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(matchId, slot), cancellationToken);
        if (board.IsFailure || board.Value.State != MatchState.InProgress)
        {
            return null;
        }

        var options = await queries.GetPlayerOptions.HandleAsync(new GetPlayerOptions(matchId, slot), cancellationToken);
        return options.IsSuccess && options.Value.Kind == PlayerOptionsKind.Evolution && options.Value.Evolution is { } evolution
            ? (board.Value, evolution)
            : null;
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
