using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Accounts;

public sealed class RecentOpponentsTests
{
    private static readonly AccountId Mark = AccountId.New();
    private static readonly AccountId Bastien = AccountId.New();
    private static readonly AccountId Julie = AccountId.New();
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private static MatchResult Game(int minutes, ResultSeat first, ResultSeat second, string? winner = "player1", string format = "3v3", bool casual = false) =>
        new(Guid.CreateVersion7().ToString(), Start.AddMinutes(minutes), format, "at-activation", "hash", 9, winner, [first, second], Casual: casual);

    private static MatchResult Against(AccountId other, int minutes, string? winner = "player1", string format = "3v3", bool casual = false) =>
        Game(minutes, new("player1", Mark, null), new("player2", other, null), winner, format, casual);

    [Fact]
    public void Each_player_met_is_listed_once_the_last_met_first()
    {
        MatchResult[] results = [Against(Bastien, 0), Against(Julie, 1), Against(Bastien, 2)];

        RecentOpponents.Of(results, Mark).Select(opponent => (opponent.Account, opponent.LastPlayed)).ShouldBe([(Bastien, Start.AddMinutes(2)), (Julie, Start.AddMinutes(1))]);
    }

    [Fact]
    public void The_record_is_counted_from_this_accounts_side_whichever_seat_it_held()
    {
        MatchResult[] results = [Against(Bastien, 0, winner: "player1"), Against(Bastien, 1, winner: "player2"), Against(Bastien, 2, winner: null),
            Game(3, new("player1", Bastien, null), new("player2", Mark, null), winner: "player2")];

        RecentOpponents.Of(results, Mark).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            opponent => opponent.Games.ShouldBe(4),
            opponent => opponent.Won.ShouldBe(2),
            opponent => opponent.Lost.ShouldBe(1),
            opponent => opponent.Drawn.ShouldBe(1));
    }

    [Fact]
    public void The_last_games_format_and_rating_are_what_a_challenge_starts_from()
    {
        MatchResult[] results = [Against(Bastien, 0, format: "3v3"), Against(Bastien, 5, format: "1v1", casual: true)];

        var opponent = RecentOpponents.Of(results, Mark).ShouldHaveSingleItem();

        opponent.LastFormat.ShouldBe("1v1");
        opponent.LastCasual.ShouldBeTrue();
    }

    [Fact]
    public void Bots_shared_devices_seats_nobody_owned_and_other_peoples_games_have_nobody_to_challenge()
    {
        MatchResult[] results = [Game(0, new("player1", Mark, null), new("player2", null, "greedy")),
            Game(1, new("player1", Mark, null), new("player2", Mark, null)),
            Game(2, new("player1", Mark, null), new("player2", null, null)),
            Game(3, new("player1", Bastien, null), new("player2", Julie, null))];

        RecentOpponents.Of(results, Mark).ShouldBeEmpty();
    }

    [Fact]
    public void Only_the_most_recent_opponents_are_kept()
    {
        MatchResult[] results = [.. Enumerable.Range(0, RecentOpponents.MostShown + 2).Select(minutes => Against(AccountId.New(), minutes))];

        var shown = RecentOpponents.Of(results, Mark);

        shown.Count.ShouldBe(RecentOpponents.MostShown);
        shown[0].LastPlayed.ShouldBe(Start.AddMinutes(RecentOpponents.MostShown + 1));
        shown[^1].LastPlayed.ShouldBe(Start.AddMinutes(2));
    }

    [Fact]
    public void A_voided_game_meets_nobody()
    {
        MatchResult[] results = [Against(Bastien, 0) with { Voided = new MatchVoid("mistake", "Opened on the wrong rules.", "op", Start) }];

        RecentOpponents.Of(results, Mark).ShouldBeEmpty();
    }
}
