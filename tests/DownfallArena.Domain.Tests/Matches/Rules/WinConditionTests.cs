using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rules;
using DownfallArena.Domain.Tests.Matches.Support;

namespace DownfallArena.Domain.Tests.Matches.Rules;

public sealed class WinConditionTests
{
    private static readonly RuleSet Rules = RuleSet.Create(2, 2, 2, 3, 2.0);

    [Fact]
    public void The_match_continues_while_both_teams_stand_below_the_cap()
    {
        var (player1, player2) = Teams();

        WinCondition.Evaluate(player1, player2, 1, Rules).ShouldBeNull();
        WinCondition.Evaluate(player1, player2, 2, Rules).ShouldBeNull();
    }

    [Fact]
    public void A_defeated_team_loses_and_two_defeated_teams_draw()
    {
        var (player1, player2) = Teams();
        Kill(player2);

        WinCondition.Evaluate(player1, player2, 1, Rules).ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.Elimination));

        Kill(player1);

        var draw = WinCondition.Evaluate(player1, player2, 1, Rules).ShouldNotBeNull();
        draw.ShouldBe(new MatchOutcome(null, MatchEndReason.Elimination));
        draw.IsDraw.ShouldBeTrue();
    }

    [Fact]
    public void Elimination_wins_over_the_round_cap_and_over_health()
    {
        var (player1, player2) = Teams();
        Kill(player1);
        player2.Creatures[0].TakeDamage(19);

        WinCondition.Evaluate(player1, player2, 3, Rules).ShouldBe(new MatchOutcome(PlayerSlot.Player2, MatchEndReason.Elimination));
    }

    [Fact]
    public void At_the_round_cap_the_highest_total_health_wins_and_equality_is_a_draw()
    {
        var (player1, player2) = Teams();

        WinCondition.Evaluate(player1, player2, 3, Rules).ShouldBe(new MatchOutcome(null, MatchEndReason.RoundCap));

        player1.Creatures[1].TakeDamage(1);
        WinCondition.Evaluate(player1, player2, 3, Rules).ShouldBe(new MatchOutcome(PlayerSlot.Player2, MatchEndReason.RoundCap));

        player2.Creatures[0].TakeDamage(5);
        WinCondition.Evaluate(player1, player2, 4, Rules).ShouldBe(new MatchOutcome(PlayerSlot.Player1, MatchEndReason.RoundCap));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var (player1, player2) = Teams();

        Should.Throw<ArgumentNullException>(() => WinCondition.Evaluate(null!, player2, 1, Rules));
        Should.Throw<ArgumentNullException>(() => WinCondition.Evaluate(player1, null!, 1, Rules));
        Should.Throw<ArgumentNullException>(() => WinCondition.Evaluate(player1, player2, 1, null!));
    }

    private static (Team Player1, Team Player2) Teams()
    {
        var creatures = Arena.FourCreatures();
        return (Team.Form(PlayerSlot.Player1, [.. creatures.Take(2)]), Team.Form(PlayerSlot.Player2, [.. creatures.Skip(2)]));
    }

    private static void Kill(Team team)
    {
        foreach (var creature in team.Creatures)
        {
            creature.TakeDamage(99);
        }
    }
}
