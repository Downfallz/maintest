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
            // A searching agent built on another agent stamps as that agent, the same way an exploring one
            // does: what it reads is the inner agent's file, not a weights file of its own (ADR 0055).
            AgentKind.Lookahead or AgentKind.Minimax when Searched(spec) is { } searched => spec with { Version = Resolve(searched).Version },
            AgentKind.Lookahead or AgentKind.Minimax when spec.Path is not null => spec with { Version = Weights(spec).Fingerprint },
            AgentKind.Policy => spec with { Version = Policy(spec).Fingerprint },
            // An exploring agent that names an inner agent is as much that agent as a heuristic or policy
            // agent is, so the stamp fingerprints what the inner one reads: two runs on different weights,
            // or on different policies, at one path have to stamp apart.
            AgentKind.Explore when Inner(spec) is { } inner => spec with { Version = Resolve(inner).Version },
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
            AgentKind.Explore => new ExploringAgent(Rate(spec), Explored(spec, rules, random), random),
            AgentKind.Lookahead => Searching(spec, rules, random, adversarial: false),
            AgentKind.Minimax => Searching(spec, rules, random, adversarial: true),
            _ => throw new InvalidOperationException($"Agent kind '{spec.Kind}' has no implementation."),
        };
    }

    /// <summary>
    /// A searching agent (ADR 0047), on the weights its path names or on the built-in ones, and playing the
    /// seats it has to guess as the agent its path names instead when it names one (ADR 0055):
    /// <c>lookahead:policy:models/clone/ci-138/policy.json</c> is the loop's own last output with a round
    /// played out on top of it, which is the operator a loop needs to climb past what it imitates. A bare
    /// path stays a weights file, so every spec written before this reads the same.
    /// </summary>
    private LookaheadAgent Searching(AgentSpec spec, RuleSet rules, IRandomSource random, bool adversarial)
    {
        if (Searched(spec) is { } searched)
        {
            return new LookaheadAgent(ScoringWeights.Default, resources, rules, adversarial, Create(searched, rules, random));
        }

        return new LookaheadAgent(spec.Path is null ? ScoringWeights.Default : Weights(spec), resources, rules, adversarial);
    }

    /// <summary>
    /// The agent a searching spec is built on, or <c>null</c> when its path is a weights file or absent. Told
    /// apart by whether the text names a kind, exactly as an exploring spec's inner agent is, so a weights
    /// file called exactly <c>greedy</c> would have to be written <c>heuristic:greedy</c>.
    /// </summary>
    private static AgentSpec? Searched(AgentSpec spec) =>
        spec.Path is { } path && NamesAKind(path) ? AgentSpec.Parse(path) : null;

    private ScoringWeights Weights(AgentSpec spec) =>
        weights.Load(spec.Path ?? throw new ArgumentException("A heuristic agent needs a weights file: 'heuristic:<path>'.", nameof(spec)));

    /// <summary>The exploration rate the spec carries after the colon: <c>explore:0.1</c>.</summary>
    private static double Rate(AgentSpec spec) =>
        double.TryParse(RateText(spec), NumberStyles.Float, CultureInfo.InvariantCulture, out var rate)
        && double.IsFinite(rate) && rate > 0 && rate <= 1
            ? rate
            : throw new ArgumentException($"An exploring agent needs a rate above 0 and at most 1: 'explore:<rate>', not '{spec}'.", nameof(spec));

    private static string RateText(AgentSpec spec) => Split(spec).Rate;

    /// <summary>
    /// The agent an exploring agent deviates from: <c>Greedy</c>, or whatever a second colon names. An
    /// exploring run is recorded so that a value policy can see what a move other than the chosen one was
    /// worth (ADR 0014), so the agent it deviates from is the one whose play is being learned. Leaving it at
    /// Greedy while the pure dataset is recorded against someone else would train the two policies of one
    /// loop on two different players.
    /// </summary>
    private IPlayerAgent Explored(AgentSpec spec, RuleSet rules, IRandomSource random) =>
        Inner(spec) is { } inner ? Create(inner, rules, random) : new GreedyAgent(resources, rules);

    /// <summary>
    /// The inner agent an exploring spec names, or <c>null</c> for the bare <c>explore:0.2</c> that wraps
    /// Greedy. A full spec is the general form — <c>explore:0.2:policy:models/clone/ci-69/policy.json</c>,
    /// which is what lets the improved policy of one turn record the dataset of the next — and a bare path
    /// stays the shorthand for a weights file that ADR 0014 and every journal entry before this one use.
    /// The two are told apart by whether the text names a kind, so a weights file called exactly
    /// <c>greedy</c>, with no directory and no extension, would have to be written <c>heuristic:greedy</c>.
    /// </summary>
    private static AgentSpec? Inner(AgentSpec spec) =>
        Split(spec).Inner switch
        {
            null => null,
            { } inner when NamesAKind(inner) => AgentSpec.Parse(inner),
            { } path => new AgentSpec(AgentKind.Heuristic, path),
        };

    private static bool NamesAKind(string inner)
    {
        var separator = inner.IndexOf(':', StringComparison.Ordinal);
        var kindText = separator < 0 ? inner : inner[..separator];
        return Enum.TryParse<AgentKind>(kindText, ignoreCase: true, out var kind) && Enum.IsDefined(kind);
    }

    /// <summary>The rate and the optional inner agent an exploring spec carries, split on the colon.</summary>
    private static (string Rate, string? Inner) Split(AgentSpec spec)
    {
        var path = spec.Path ?? string.Empty;
        var separator = path.IndexOf(':', StringComparison.Ordinal);
        return separator < 0 ? (path, null) : (path[..separator], path[(separator + 1)..]);
    }

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

        return new PolicyAgent(policy, new ObservationBuilder(schema, resources), new ActionEncoder(schema), new CandidateTerms(resources, rules));
    }
}
