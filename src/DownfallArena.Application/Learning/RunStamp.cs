using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;

namespace DownfallArena.Application.Learning;

/// <summary>
/// The identity of a run, present on every learning artifact so two runs compare on one axis at a time:
/// engine version, content hash, rule set, feature schema id, agents, base seed (ADR 0013).
/// </summary>
public sealed record RunStamp
{
    public required string EngineVersion { get; init; }

    public required string ContentHash { get; init; }

    public required RuleSetStamp RuleSet { get; init; }

    public required string FeatureSchema { get; init; }

    public required string Player1Agent { get; init; }

    public required string Player2Agent { get; init; }

    public required int BaseSeed { get; init; }

    public static RunStamp Create(
        EngineVersion engine,
        IGameResources resources,
        RuleSet ruleSet,
        FeatureSchema schema,
        string player1Agent,
        string player2Agent,
        int baseSeed)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(player1Agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(player2Agent);

        return new RunStamp
        {
            EngineVersion = engine.ToString(),
            ContentHash = resources.Version,
            RuleSet = RuleSetStamp.Of(ruleSet),
            FeatureSchema = schema.Id,
            Player1Agent = player1Agent,
            Player2Agent = player2Agent,
            BaseSeed = baseSeed,
        };
    }

    /// <summary>The axes on which two stamps differ, in the order content, engine, rule set, schema, agents, seed.</summary>
    public IReadOnlyList<string> DifferencesFrom(RunStamp other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var differences = new List<string>();
        Add(differences, "content", ContentHash != other.ContentHash);
        Add(differences, "engine", EngineVersion != other.EngineVersion);
        Add(differences, "rules", RuleSet != other.RuleSet);
        Add(differences, "schema", FeatureSchema != other.FeatureSchema);
        Add(differences, "agents", Player1Agent != other.Player1Agent || Player2Agent != other.Player2Agent);
        Add(differences, "seed", BaseSeed != other.BaseSeed);
        return differences;
    }

    private static void Add(List<string> differences, string axis, bool differs)
    {
        if (differs)
        {
            differences.Add(axis);
        }
    }
}
