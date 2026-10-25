using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// The heuristic agent with the built-in weights: the baseline every learned agent is measured against, and the
/// agent behind the benchmark digest. Deterministic for a seed: its only draws are the seat's dice, which break a
/// tie between two purchases worth the same (ADR 0113).
/// </summary>
public sealed class GreedyAgent(IGameResources resources, RuleSet rules, IRandomSource? random = null) : IPlayerAgent
{
    private readonly HeuristicAgent _inner = new(ScoringWeights.Default, resources, rules, random);

    /// <summary>Whether a purchase tie rolls the seat's dice (ADR 0113).</summary>
    public bool RollsDice => _inner.RollsDice;

    public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => _inner.DecideEvolution(board, options);

    public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => _inner.DecideSpeed(board, creature);

    public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => _inner.DecideTieOrder(board, options);

    public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => _inner.DecideIntent(board, intentOption);

    public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => _inner.DecideTargets(board, options);

    public ChosenAction DecideAction(PlayerBoardState board, ActionOptions options) => _inner.DecideAction(board, options);
}
