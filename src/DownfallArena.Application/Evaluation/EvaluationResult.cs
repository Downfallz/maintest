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

    /// <summary>
    /// True when both agents are the same spec. The mirrored pass then replays the same matches — an agent is
    /// seeded from the match seed and the slot, not from which agent it is — so each side reports the totals of
    /// both players and the split is one half by construction, not by measurement. The spell outcomes still
    /// mean something: they are about the content, not about who played it.
    /// </summary>
    public bool SelfPlay { get; init; }

    /// <summary>Every spell against the outcomes of the sides that declared it, best score first.</summary>
    public IReadOnlyList<SpellOutcome> SpellOutcomes { get; init; } = [];

    public required IReadOnlyList<SeedPair> Pairs { get; init; }

    public double DrawRate => Matches == 0 ? 0 : (double)Draws / Matches;
}
