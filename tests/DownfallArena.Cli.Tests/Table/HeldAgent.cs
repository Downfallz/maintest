using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A bot that can be made to stop and think at its next decision. At a stepped table a bot's turn is the
/// match's turn, so while it thinks the match stays in the round it was asked in: what the session has already
/// read can still be asked for, and anything that needs the match's turn waits behind it.
/// </summary>
internal sealed class HeldAgent(IPlayerAgent inner) : IPlayerAgent
{
    private volatile TaskCompletionSource? _thinking;

    public void Hold() => _thinking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _thinking?.TrySetResult();

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => Thought().DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Thought().DecideSpeed(board, creature);

    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => Thought().DecideTieOrder(board, options);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => Thought().DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => Thought().DecideTargets(board, options);

    public ChosenAction DecideAction(PlayerBoardState board, ActionOptions options) => Thought().DecideAction(board, options);

    private IPlayerAgent Thought()
    {
        _thinking?.Task.Wait(TimeSpan.FromSeconds(30));
        return inner;
    }
}
