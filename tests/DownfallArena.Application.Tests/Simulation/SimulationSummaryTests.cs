using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Simulation;

public sealed class SimulationSummaryTests
{
    [Fact]
    public void The_summary_counts_outcomes_and_averages_rounds_and_health()
    {
        var results = new List<MatchResult>
        {
            Result(0, PlayerSlot.Player1, MatchEndReason.Elimination, rounds: 4, health1: 10, health2: 0),
            Result(1, PlayerSlot.Player1, MatchEndReason.RoundCap, rounds: 8, health1: 6, health2: 4),
            Result(2, PlayerSlot.Player2, MatchEndReason.Elimination, rounds: 6, health1: 0, health2: 12),
            Result(3, null, MatchEndReason.RoundCap, rounds: 2, health1: 8, health2: 8),
        };

        var summary = SimulationSummary.Of(results);

        summary.Matches.ShouldBe(4);
        summary.Player1Wins.ShouldBe(2);
        summary.Player2Wins.ShouldBe(1);
        summary.Draws.ShouldBe(1);
        summary.Player1WinRate.ShouldBe(0.5);
        summary.Player2WinRate.ShouldBe(0.25);
        summary.DrawRate.ShouldBe(0.25);
        summary.AverageRounds.ShouldBe(5);
        summary.MinRounds.ShouldBe(2);
        summary.MaxRounds.ShouldBe(8);
        summary.AveragePlayer1RemainingHealth.ShouldBe(6);
        summary.AveragePlayer2RemainingHealth.ShouldBe(6);
    }

    [Fact]
    public void An_empty_summary_is_all_zeros()
    {
        var summary = SimulationSummary.Of([]);

        summary.ShouldBe(new SimulationSummary
        {
            Matches = 0,
            Player1Wins = 0,
            Player2Wins = 0,
            Draws = 0,
            AverageRounds = 0,
            MinRounds = 0,
            MaxRounds = 0,
            AveragePlayer1RemainingHealth = 0,
            AveragePlayer2RemainingHealth = 0,
        });
        summary.DrawRate.ShouldBe(0);
    }

    [Fact]
    public void Null_results_are_rejected()
    {
        Should.Throw<ArgumentNullException>(() => SimulationSummary.Of(null!));
    }

    private static MatchResult Result(int index, PlayerSlot? winner, MatchEndReason reason, int rounds, int health1, int health2) =>
        new(index, 100 + index, MatchId.New(), new MatchOutcome(winner, reason), rounds, health1, health2);
}
