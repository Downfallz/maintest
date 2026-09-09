using System.Collections.Concurrent;
using DownfallArena.Application.Agents.Ports;
using DownfallArena.Application.Learning;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Application.Agents;

/// <summary>
/// The registry of the agents the engine ships: one case per <see cref="AgentKind"/>.
/// </summary>
public sealed class AgentFactory(IGameResources resources, IScoringWeightsSource weights, IPolicySource policies) : IAgentFactory
{
    private readonly ConcurrentDictionary<RuleSet, FeatureSchema> _schemas = new();

    public AgentSpec Resolve(AgentSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Kind switch
        {
            AgentKind.Heuristic => spec with { Version = Weights(spec).Fingerprint },
            AgentKind.Policy => spec with { Version = Policy(spec).Fingerprint },
            _ => spec,
        };
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
            AgentKind.Policy => Trained(spec, rules),
            _ => throw new InvalidOperationException($"Agent kind '{spec.Kind}' has no implementation."),
        };
    }

    private ScoringWeights Weights(AgentSpec spec) =>
        weights.Load(spec.Path ?? throw new ArgumentException("A heuristic agent needs a weights file: 'heuristic:<path>'.", nameof(spec)));

    private PolicyFile Policy(AgentSpec spec) =>
        policies.Load(spec.Path ?? throw new ArgumentException("A policy agent needs a policy file: 'policy:<path>'.", nameof(spec)));

    /// <summary>
    /// A policy only plays under the exact feature schema it was trained under: the same content and rule set
    /// give the same id, anything else would feed its rows another layout.
    /// </summary>
    private PolicyAgent Trained(AgentSpec spec, RuleSet rules)
    {
        var policy = Policy(spec);
        var schema = _schemas.GetOrAdd(rules, ruleSet => FeatureSchema.Build(resources, ruleSet));
        if (policy.SchemaId != schema.Id || policy.FeatureNames.Count != schema.Length)
        {
            throw new InvalidDataException($"Policy '{spec.Path}' was trained under feature schema '{policy.SchemaId}'; this content and rule set give '{schema.Id}'. Retrain it, or evaluate it on the content it knows.");
        }

        return new PolicyAgent(policy, new ObservationBuilder(schema, resources), new ActionEncoder(schema));
    }
}
