using DownfallArena.Application.Learning;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// A mirrored evaluation of two agents on a seed list: one report per agent, the shared numbers, and every
/// seed pair, stamped with the run that produced it (ADR 0013).
/// </summary>
public sealed record EvaluationResult
{
    public required RunStamp Stamp { get; init; }

    public required AgentReport AgentA { get; init; }

    public required AgentReport AgentB { get; init; }

    public required int Matches { get; init; }

    public required int Draws { get; init; }

    public required double AverageRounds { get; init; }

    /// <summary>The share of matches that ended by the round cap rather than by elimination.</summary>
    public required double RoundCapShare { get; init; }

    public required IReadOnlyList<SeedPair> Pairs { get; init; }

    public double DrawRate => Matches == 0 ? 0 : (double)Draws / Matches;
}
