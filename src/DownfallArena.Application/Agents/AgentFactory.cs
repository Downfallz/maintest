using DownfallArena.Application.Agents.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// The registry of the agents the engine ships: one case per <see cref="AgentKind"/>.
/// </summary>
public sealed class AgentFactory(IGameResources resources, IScoringWeightsSource weights) : IAgentFactory
{
    public AgentSpec Resolve(AgentSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Kind == AgentKind.Heuristic ? spec with { Version = Weights(spec).Fingerprint } : spec;
    }

    public IPlayerAgent Create(AgentSpec spec, RuleSet rules, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(random);

        return spec.Kind switch
        {
            AgentKind.Random => new RandomAgent(random),
            AgentKind.Greedy => new GreedyAgent(resources, rules),
            AgentKind.Heuristic => new HeuristicAgent(Weights(spec), resources, rules),
            _ => throw new InvalidOperationException($"Agent kind '{spec.Kind}' has no implementation."),
        };
    }

    private ScoringWeights Weights(AgentSpec spec) =>
        weights.Load(spec.Path ?? throw new ArgumentException("A heuristic agent needs a weights file: 'heuristic:<path>'.", nameof(spec)));
}
