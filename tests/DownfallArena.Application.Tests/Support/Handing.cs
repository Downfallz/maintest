using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// A seat that changes hands from the top of a round, as an agent the driver can be given: every decision of a
/// round before it is the first occupant's, every decision from it on the second's.
/// </summary>
internal sealed class Handing(IPlayerAgent first, IPlayerAgent then, int fromRound) : IPlayerAgent
{
    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => Sitting(board).DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Sitting(board).DecideSpeed(board, creature);

    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => Sitting(board).DecideTieOrder(board, options);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => Sitting(board).DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => Sitting(board).DecideTargets(board, options);

    public ChosenAction DecideAction(PlayerBoardState board, ActionOptions options) => Sitting(board).DecideAction(board, options);

    private IPlayerAgent Sitting(PlayerBoardState board) => (board.RoundNumber ?? 0) >= fromRound ? then : first;
}
