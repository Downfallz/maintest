using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Simulation;

/// <summary>
/// The balance numbers of a batch: win rates by slot, draw rate, round counts, and remaining health.
/// </summary>
public sealed record SimulationSummary
{
    public required int Matches { get; init; }

    public required int Player1Wins { get; init; }

    public required int Player2Wins { get; init; }

    public required int Draws { get; init; }

    public required double AverageRounds { get; init; }

    public required int MinRounds { get; init; }

    public required int MaxRounds { get; init; }

    public required double AveragePlayer1RemainingHealth { get; init; }

    public required double AveragePlayer2RemainingHealth { get; init; }

    public double Player1WinRate => Rate(Player1Wins);

    public double Player2WinRate => Rate(Player2Wins);

    public double DrawRate => Rate(Draws);

    public static SimulationSummary Of(IReadOnlyList<MatchResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return new SimulationSummary
        {
            Matches = results.Count,
            Player1Wins = results.Count(result => result.Outcome.Winner == PlayerSlot.Player1),
            Player2Wins = results.Count(result => result.Outcome.Winner == PlayerSlot.Player2),
            Draws = results.Count(result => result.Outcome.IsDraw),
            AverageRounds = results.Count == 0 ? 0 : results.Average(result => result.Rounds),
            MinRounds = results.Count == 0 ? 0 : results.Min(result => result.Rounds),
            MaxRounds = results.Count == 0 ? 0 : results.Max(result => result.Rounds),
            AveragePlayer1RemainingHealth = results.Count == 0 ? 0 : results.Average(result => result.Player1RemainingHealth),
            AveragePlayer2RemainingHealth = results.Count == 0 ? 0 : results.Average(result => result.Player2RemainingHealth),
        };
    }

    private double Rate(int count) => Matches == 0 ? 0 : (double)count / Matches;
}
