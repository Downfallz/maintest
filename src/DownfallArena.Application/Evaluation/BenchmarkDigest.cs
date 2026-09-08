using DownfallArena.Application.Simulation;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// The outcomes of the benchmark seeds under the baseline agents, for one content hash: what CI compares
/// against the committed digest to detect an engine change (ADR 0013, decision I). Plain values only, so the
/// file reads back without the engine's converters.
/// </summary>
public sealed record BenchmarkDigest
{
    public required string ContentHash { get; init; }

    /// <summary>The engine that produced the digest, for the journal; not compared.</summary>
    public required string EngineVersion { get; init; }

    public required string AgentA { get; init; }

    public required string AgentB { get; init; }

    public required IReadOnlyList<BenchmarkEntry> Entries { get; init; }

    public static BenchmarkDigest Of(EvaluationResult evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        return new BenchmarkDigest
        {
            ContentHash = evaluation.Stamp.ContentHash,
            EngineVersion = evaluation.Stamp.EngineVersion,
            AgentA = evaluation.AgentA.Agent,
            AgentB = evaluation.AgentB.Agent,
            Entries = [.. evaluation.Pairs.SelectMany(pair => new[] { BenchmarkEntry.Of(pair.Seed, "AB", pair.AFirst), BenchmarkEntry.Of(pair.Seed, "BA", pair.BFirst) })],
        };
    }

    /// <summary>The entries that differ from another digest, one line each; empty when the outcomes are the same.</summary>
    public IReadOnlyList<string> DifferencesFrom(BenchmarkDigest other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var differences = new List<string>();
        if (ContentHash != other.ContentHash)
        {
            differences.Add($"content hash {other.ContentHash} versus {ContentHash}");
        }

        if (AgentA != other.AgentA || AgentB != other.AgentB)
        {
            differences.Add($"agents {other.AgentA} versus {other.AgentB}, expected {AgentA} versus {AgentB}");
        }

        var theirs = other.Entries.ToDictionary(entry => (entry.Seed, entry.Order));
        foreach (var entry in Entries)
        {
            if (!theirs.TryGetValue((entry.Seed, entry.Order), out var counterpart))
            {
                differences.Add($"seed {entry.Seed} {entry.Order}: missing");
            }
            else if (entry != counterpart)
            {
                differences.Add($"seed {entry.Seed} {entry.Order}: {counterpart} became {entry}");
            }
        }

        differences.AddRange(other.Entries.Where(entry => !Entries.Any(mine => mine.Seed == entry.Seed && mine.Order == entry.Order)).Select(entry => $"seed {entry.Seed} {entry.Order}: no longer played"));
        return differences;
    }
}
