using DownfallArena.Application.Accounts;
using DownfallArena.Application.Leaderboard;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Leaderboard;

public sealed class StandingsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 20, 0, 0, TimeSpan.Zero);
    private static readonly AccountId Mark = AccountId.New();
    private static readonly AccountId Bastien = AccountId.New();
    private static readonly AccountId Lune = AccountId.New();

    private static MatchResult Game(AccountId first, AccountId second, string? winner, int minutes) =>
        new(Guid.CreateVersion7().ToString(), Start.AddMinutes(minutes), "3v3", "at-activation", "hash", 10, winner, [new("player1", first, null), new("player2", second, null)]);

    private static MatchResult AgainstBot(AccountId person, string agent, string? winner, int minutes) =>
        new(Guid.CreateVersion7().ToString(), Start.AddMinutes(minutes), "3v3", "at-activation", "hash", 10, winner, [new("player1", person, null), new("player2", null, agent)]);

    [Fact]
    public void Players_are_ranked_by_what_their_ranked_games_came_to()
    {
        var standings = Standings.Compute(
            [Game(Mark, Bastien, "player1", 0), Game(Mark, Bastien, "player1", 1), Game(Bastien, Lune, "player1", 2)],
            Start.AddMinutes(3));

        var mark = standings.Players.Single(player => player.Account == Mark);
        mark.Wins.ShouldBe(2);
        mark.Losses.ShouldBe(0);
        standings.Players.Single(player => player.Account == Bastien).Games.ShouldBe(3);
        standings.Players.Select(player => player.Account).ShouldBe([Mark, Bastien, Lune]);
    }

    [Fact]
    public void A_draw_counts_as_half_for_each_side()
    {
        var standings = Standings.Compute([Game(Mark, Bastien, winner: null, 0)], Start);

        standings.Players.ShouldAllBe(player => player.Draws == 1);
        standings.Players[0].Rating.Rating.ShouldBe(1500, 0.001);
    }

    [Fact]
    public void A_player_new_to_the_ladder_is_provisional_until_enough_games_settle_their_rating()
    {
        var games = Enumerable.Range(0, 30).Select(index => Game(Mark, index % 2 == 0 ? Bastien : Lune, index % 3 == 0 ? "player2" : "player1", index)).ToList();

        Standings.Compute(games.Take(1), Start).Players.ShouldAllBe(player => player.Provisional);
        Standings.Compute(games, Start.AddMinutes(30)).Players.Single(player => player.Account == Mark).Provisional.ShouldBeFalse();
    }

    [Fact]
    public void Games_against_a_bot_and_on_a_shared_device_are_never_rated()
    {
        var standings = Standings.Compute(
            [AgainstBot(Mark, "greedy", "player1", 0), AgainstBot(Mark, "greedy", "player2", 1), AgainstBot(Mark, "lookahead", null, 2), Game(Mark, Mark, "player1", 3)],
            Start);

        standings.Players.ShouldBeEmpty();
        standings.AgainstBots(Mark).ShouldBe([new BotRecord("greedy", 1, 1, 0), new BotRecord("lookahead", 0, 0, 1)]);
        standings.AgainstBots(Bastien).ShouldBeEmpty();
    }

    [Fact]
    public void A_player_who_stops_playing_grows_less_certain_day_by_day()
    {
        var games = new[] { Game(Mark, Bastien, "player1", 0) };

        var now = Standings.Compute(games, Start).Players[0].Rating.Deviation;
        var later = Standings.Compute(games, Start.AddDays(60)).Players[0].Rating.Deviation;

        later.ShouldBeGreaterThan(now);
    }
}
