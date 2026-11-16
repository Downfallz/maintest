using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Whoever decides for a seat, with the round of every board they are shown noted on the way: how a stepped
/// table knows the last round each seat decided anything in, which is what says whether a seat can still change
/// hands from the top of a round.
/// </summary>
internal sealed class ReachingAgent(IPlayerAgent inner, Action<int?> reached) : IPlayerAgent
{
    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => Noted(board).DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Noted(board).DecideSpeed(board, creature);

    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => Noted(board).DecideTieOrder(board, options);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => Noted(board).DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => Noted(board).DecideTargets(board, options);

    public ChosenAction DecideAction(PlayerBoardState board, ActionOptions options) => Noted(board).DecideAction(board, options);

    private IPlayerAgent Noted(PlayerBoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);
        reached(board.RoundNumber);
        return inner;
    }
}
