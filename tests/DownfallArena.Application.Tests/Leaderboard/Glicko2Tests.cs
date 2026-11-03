using DownfallArena.Application.Leaderboard;

namespace DownfallArena.Application.Tests.Leaderboard;

public sealed class Glicko2Tests
{
    /// <summary>The worked example of Glickman's "Example of the Glicko-2 system", to the paper's precision.</summary>
    [Fact]
    public void The_papers_worked_example_comes_out_as_the_paper_says()
    {
        var player = new GlickoRating(1500, 200, 0.06);

        var rated = Glicko2.Rate(player, [(new GlickoRating(1400, 30, 0.06), 1), (new GlickoRating(1550, 100, 0.06), 0), (new GlickoRating(1700, 300, 0.06), 0)]);

        rated.Rating.ShouldBe(1464.06, 0.01);
        rated.Deviation.ShouldBe(151.52, 0.01);
        rated.Volatility.ShouldBe(0.05999, 0.00001);
    }

    [Fact]
    public void A_win_raises_a_rating_and_a_loss_lowers_it_and_both_grow_more_certain()
    {
        var rival = GlickoRating.Unrated;

        var won = Glicko2.Rate(GlickoRating.Unrated, [(rival, 1)]);
        var lost = Glicko2.Rate(GlickoRating.Unrated, [(rival, 0)]);

        won.Rating.ShouldBeGreaterThan(1500);
        lost.Rating.ShouldBeLessThan(1500);
        won.Deviation.ShouldBeLessThan(Glicko2.MaximumDeviation);
    }

    [Fact]
    public void A_period_without_games_keeps_the_rating_and_grows_the_deviation_up_to_its_maximum()
    {
        var player = new GlickoRating(1600, 60, 0.06);

        var idle = Glicko2.Idle(player, 30);

        idle.Rating.ShouldBe(1600);
        idle.Deviation.ShouldBeGreaterThan(60);
        Glicko2.Idle(player, 1_000_000).Deviation.ShouldBe(Glicko2.MaximumDeviation);
        Glicko2.Rate(player, []).ShouldBe(Glicko2.Idle(player, 1));
    }
}
