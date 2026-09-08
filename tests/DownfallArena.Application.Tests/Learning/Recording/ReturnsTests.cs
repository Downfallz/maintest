using DownfallArena.Application.Learning.Recording;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Tests.Learning.Recording;

public sealed class ReturnsTests
{
    private static readonly MatchOutcome Player1Wins = new(PlayerSlot.Player1, MatchEndReason.Elimination);
    private static readonly MatchOutcome Draw = new(null, MatchEndReason.RoundCap);

    [Fact]
    public void A_win_is_one_a_loss_minus_one_a_draw_zero_before_the_margin()
    {
        Returns.Of(Player1Wins, PlayerSlot.Player1, 0, 0, 40).ShouldBe(1.0);
        Returns.Of(Player1Wins, PlayerSlot.Player2, 0, 0, 40).ShouldBe(-1.0);
        Returns.Of(Draw, PlayerSlot.Player1, 0, 0, 40).ShouldBe(0.0);
    }

    [Fact]
    public void The_health_margin_adds_a_tenth_of_its_fraction_of_the_total_health()
    {
        Returns.Of(Player1Wins, PlayerSlot.Player1, 10, 0, 40).ShouldBe(1.025, 1e-9);
        Returns.Of(Draw, PlayerSlot.Player1, 5, 15, 40).ShouldBe(-0.025, 1e-9);
        Returns.Of(Draw, PlayerSlot.Player1, 5, 15, 0).ShouldBe(0.0);
    }

    [Fact]
    public void The_two_players_returns_sum_to_zero()
    {
        var player1 = Returns.Of(Player1Wins, PlayerSlot.Player1, 12, 3, 40);
        var player2 = Returns.Of(Player1Wins, PlayerSlot.Player2, 3, 12, 40);

        (player1 + player2).ShouldBe(0.0, 1e-12);
    }

    [Fact]
    public void Invalid_inputs_are_rejected()
    {
        Should.Throw<ArgumentNullException>(() => Returns.Of(null!, PlayerSlot.Player1, 0, 0, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => Returns.Of(Draw, PlayerSlot.Player1, -1, 0, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => Returns.Of(Draw, PlayerSlot.Player1, 0, -1, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => Returns.Of(Draw, PlayerSlot.Player1, 0, 0, -1));
    }
}
