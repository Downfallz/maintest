using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Evaluation;

public sealed class SeedPairTests
{
    [Fact]
    public void Scores_wins_and_health_follow_agent_A_across_the_swap()
    {
        // A wins as player 1, then loses as player 2 to B who is player 1.
        var pair = new SeedPair(7, Result(PlayerSlot.Player1, 10, 0), Result(PlayerSlot.Player1, 8, 2));

        pair.ScoreOfA.ShouldBe(0.5);
        pair.WinsOfA.ShouldBe(1);
        pair.WinsOfB.ShouldBe(1);
        pair.Draws.ShouldBe(0);
        pair.RemainingHealthOfA.ShouldBe(10 + 2);
        pair.RemainingHealthOfB.ShouldBe(0 + 8);
    }

    [Fact]
    public void A_draw_counts_one_half_for_each()
    {
        var pair = new SeedPair(7, Result(null, 5, 5), Result(PlayerSlot.Player2, 0, 9));

        pair.ScoreOfA.ShouldBe(0.75);
        pair.WinsOfA.ShouldBe(1);
        pair.WinsOfB.ShouldBe(0);
        pair.Draws.ShouldBe(1);
    }

    private static MatchResult Result(PlayerSlot? winner, int player1Health, int player2Health) => new()
    {
        Index = 0,
        Seed = 7,
        MatchId = MatchId.New(),
        ContentHash = "test-content",
        Outcome = new MatchOutcome(winner, winner is null ? MatchEndReason.RoundCap : MatchEndReason.Elimination),
        Rounds = 3,
        Player1RemainingHealth = player1Health,
        Player2RemainingHealth = player2Health,
    };
}
