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
/// can do, lets their agent decide, submits, and drives the resolution. An agent that produces a refused
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

    /// <summary>The creatures still to declare, in the order they will act. Any without a slot go last, in the
    /// order the options gave them, so a creature that is not on the timeline is neither lost nor reordered.</summary>
    private static IEnumerable<IntentOption> InTimelineOrder(IReadOnlyList<IntentOption> options, PlayerBoardState board)
    {
        var position = new Dictionary<CreatureId, int>();
        for (var index = 0; index < board.Timeline.Count; index++)
        {
            position[board.Timeline[index].Creature] = index;
        }

        return options.OrderBy(option => position.TryGetValue(option.Creature, out var slot) ? slot : int.MaxValue);
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
            case PlayerOptionsKind.Intent:
                // Re-read the board for each creature, because the previous one's intent is on it. A player
                // declares in sequence and knows what they have already declared; the projection has carried
                // those intents all along (`PlayerBoardState.Intents`) and this loop used to hand every
                // creature the same board from before the first of them chose (ADR 0039).
                //
                // In timeline order, so that a creature deciding has already heard from every ally that acts
                // before it. Asked in any other order, an ally that swings first may not have chosen yet, and
                // what it is about to do cannot be read at all.
                foreach (var option in InTimelineOrder(Section(options.Intent).Creatures, board))
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
            case PlayerOptionsKind.Resolution:
                Accept(await commands.ResolveNextAction.HandleAsync(new ResolveNextAction(matchId), cancellationToken));
                return true;
            default:
                return false;
        }
    }

    private async Task EvolveAsync(MatchId matchId, PlayerSlot slot, IPlayerAgent agent, PlayerBoardState board, EvolutionOptions options, CancellationToken cancellationToken)
    {
        var decision = agent.DecideEvolution(board, options);
        if (decision.Choice is { } choice)
        {
            Accept(await commands.SubmitEvolutionChoice.HandleAsync(new SubmitEvolutionChoice(matchId, slot, choice.Creature, choice.Spell), cancellationToken));
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

    private static void Accept(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"An agent's decision was refused: {result.Error.Code} ({result.Error.Message}).");
        }
    }
}
