using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Accounts;

public sealed class FinishedGamesTests
{
    private static readonly AccountId Mark = AccountId.New();
    private static readonly AccountId Bastien = AccountId.New();
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private static MatchResult Against(ResultSeat other, int minutes) =>
        new(Guid.CreateVersion7().ToString(), Start.AddMinutes(minutes), "3v3", "at-activation", "hash", 9, "player1", [new("player1", Mark, null), other]);

    private static MatchResult AgainstBot(int minutes) => Against(new("player2", null, "greedy"), minutes);

    [Fact]
    public void An_accounts_games_against_bots_are_listed_apart_newest_first_a_page_at_a_time()
    {
        MatchResult[] results = [.. Enumerable.Range(0, 8).Select(AgainstBot), Against(new("player2", Bastien, null), 100)];

        var first = FinishedGames.Page(results, Mark, againstBots: true, page: 0);
        var second = FinishedGames.Page(results, Mark, againstBots: true, page: 1);

        first.Total.ShouldBe(8);
        first.Pages.ShouldBe(2);
        first.Items.Select(result => result.FinishedAt).ShouldBe([.. Enumerable.Range(2, 6).Reverse().Select(minutes => Start.AddMinutes(minutes))]);
        second.Items.Select(result => result.FinishedAt).ShouldBe([Start.AddMinutes(1), Start]);
    }

    [Fact]
    public void Games_against_players_hold_every_game_no_bot_played_in_a_shared_device_included_and_none_of_others()
    {
        MatchResult[] results = [AgainstBot(0), Against(new("player2", Bastien, null), 1), Against(new("player2", Mark, null), 2),
            new(Guid.CreateVersion7().ToString(), Start, "3v3", "at-activation", "hash", 9, null, [new("player1", Bastien, null), new("player2", null, "greedy")])];

        FinishedGames.Page(results, Mark, againstBots: false, page: 0).Items.Select(result => result.FinishedAt).ShouldBe([Start.AddMinutes(2), Start.AddMinutes(1)]);
    }

    [Fact]
    public void A_page_past_the_end_shows_the_last_and_no_game_is_one_empty_page()
    {
        FinishedGames.Page([.. Enumerable.Range(0, 7).Select(AgainstBot)], Mark, againstBots: true, page: 9).ShouldSatisfyAllConditions(
            page => page.Page.ShouldBe(1),
            page => page.Items.ShouldHaveSingleItem());
        FinishedGames.Page([], Mark, againstBots: true, page: 0).ShouldBe(new FinishedPage(0, 1, 0, []), new FinishedPageComparer());
    }

    private sealed class FinishedPageComparer : IEqualityComparer<FinishedPage>
    {
        public bool Equals(FinishedPage? x, FinishedPage? y) => x is not null && y is not null && x.Total == y.Total && x.Pages == y.Pages && x.Page == y.Page && x.Items.SequenceEqual(y.Items);

        public int GetHashCode(FinishedPage obj) => HashCode.Combine(obj.Total, obj.Pages, obj.Page);
    }
}
