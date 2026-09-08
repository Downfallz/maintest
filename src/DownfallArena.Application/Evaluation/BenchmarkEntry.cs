using DownfallArena.Application.Simulation;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// One benchmark match: the seed, which agent was player 1 ("AB" or "BA"), and how it ended.
/// </summary>
public sealed record BenchmarkEntry(int Seed, string Order, string Winner, string Reason, int Rounds, int Player1Health, int Player2Health)
{
    public const string Draw = "Draw";

    public static BenchmarkEntry Of(int seed, string order, MatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new BenchmarkEntry(seed, order, result.Outcome.Winner?.ToString() ?? Draw, result.Outcome.Reason.ToString(), result.Rounds, result.Player1RemainingHealth, result.Player2RemainingHealth);
    }

    public override string ToString() => $"{Winner} by {Reason} in {Rounds} rounds ({Player1Health}/{Player2Health})";
}
