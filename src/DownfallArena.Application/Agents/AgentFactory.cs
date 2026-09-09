using System.Collections.Concurrent;
using System.Globalization;
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
            AgentKind.Explore => new ExploringAgent(Rate(spec), new GreedyAgent(resources, rules), new RandomAgent(random), random),
            _ => throw new InvalidOperationException($"Agent kind '{spec.Kind}' has no implementation."),
        };
    }

    private ScoringWeights Weights(AgentSpec spec) =>
        weights.Load(spec.Path ?? throw new ArgumentException("A heuristic agent needs a weights file: 'heuristic:<path>'.", nameof(spec)));

    /// <summary>The exploration rate the spec carries after the colon: <c>explore:0.1</c>.</summary>
    private static double Rate(AgentSpec spec) =>
        double.TryParse(spec.Path, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate)
        && double.IsFinite(rate) && rate > 0 && rate <= 1
            ? rate
            : throw new ArgumentException($"An exploring agent needs a rate above 0 and at most 1: 'explore:<rate>', not '{spec}'.", nameof(spec));

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
        if (policy.SchemaId != schema.Id)
        {
            throw new InvalidDataException($"Policy '{spec.Path}' was trained under feature schema '{policy.SchemaId}'; this content and rule set give '{schema.Id}'. Retrain it, or evaluate it on the content it knows.");
        }

        // The id is a fingerprint of the names, but an edited file can carry the id with other names: the rows
        // are only meaningful on the exact feature order they were trained on.
        if (!policy.FeatureNames.SequenceEqual(schema.FeatureNames, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Policy '{spec.Path}' names its features in another order than feature schema '{schema.Id}'.");
        }

        return new PolicyAgent(policy, new ObservationBuilder(schema, resources), new ActionEncoder(schema));
    }
}
