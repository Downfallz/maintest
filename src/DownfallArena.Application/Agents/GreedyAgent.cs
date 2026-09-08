using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Agents;

/// <summary>
/// The heuristic agent with the built-in weights: the deterministic baseline every learned agent is measured
/// against, and the agent behind the benchmark digest.
/// </summary>
public sealed class GreedyAgent(IGameResources resources, RuleSet rules) : IPlayerAgent
{
    private readonly HeuristicAgent _inner = new(ScoringWeights.Default, resources, rules);

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => _inner.DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => _inner.DecideSpeed(board, creature);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => _inner.DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => _inner.DecideTargets(board, options);
}
