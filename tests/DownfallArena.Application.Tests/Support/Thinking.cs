using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// A Player agent that stops to think at its first decision until it is released, and says when it has been
/// asked. It gives up thinking after a few seconds, so a test that never releases it fails rather than hangs.
/// </summary>
internal sealed class Thinking(IPlayerAgent inner) : IPlayerAgent
{
    private readonly TaskCompletionSource _asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the agent is asked its first decision.</summary>
    public Task Asked => _asked.Task;

    public void Release() => _released.TrySetResult();

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => Thought().DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Thought().DecideSpeed(board, creature);

    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => Thought().DecideTieOrder(board, options);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => Thought().DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => Thought().DecideTargets(board, options);

    public ChosenAction DecideAction(PlayerBoardState board, ActionOptions options) => Thought().DecideAction(board, options);

    private IPlayerAgent Thought()
    {
        _asked.TrySetResult();
        _released.Task.Wait(TimeSpan.FromSeconds(5));
        return inner;
    }
}
