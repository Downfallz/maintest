using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Players;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Tests.Support;
using DownfallArena.SharedKernel.Identifiers;
using NSubstitute;

namespace DownfallArena.Application.Tests.Accounts.Players;

public sealed class PlayerDirectoryTests
{
    private static readonly DateTimeOffset Start = FixedTimeProvider.Default;

    private readonly MemoryAccountStore _accounts = new();
    private readonly MemoryActivityStore _activity = new();
    private readonly IMatchResults _results = Substitute.For<IMatchResults>();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private PlayerDirectory Directory() => new(_accounts, new AccountActivityLog(_activity, new FixedTimeProvider(Start)), _results, new FixedTimeProvider(Start));

    private Account Add(string name, int createdDaysAgo = 0)
    {
        var account = new Account(AccountId.New(), new ExternalIdentity(ExternalIdentity.Development, name.ToLowerInvariant()), Username.Restore(name), Start.AddDays(-createdDaysAgo), 1);
        _accounts.Accounts[account.Id] = account;
        return account;
    }

    private void Results(params MatchResult[] results) => _results.ListAsync(Arg.Any<CancellationToken>()).Returns(results);

    private static MatchResult Game(ResultSeat first, ResultSeat second, string? winner = "player1", bool casual = false) =>
        new(Guid.CreateVersion7().ToString(), Start.AddHours(-1), "3v3", "at-activation", "hash", 9, winner, [first, second], Casual: casual);

    private static PlayerSummary Summary(string name, int games = 0, DateTimeOffset? seen = null, DateTimeOffset? created = null) =>
        new(new Account(AccountId.New(), new ExternalIdentity(ExternalIdentity.Development, name), Username.Restore(name), created ?? Start, 1),
            seen is null ? null : new AccountActivity(AccountId.New(), null, seen), games, 0, 0, 0, 0, null);

    [Fact]
    public async Task Every_account_is_listed_with_its_activity()
    {
        var mark = Add("Mark");
        Add("Julie");
        _activity.Kept[mark.Id] = new AccountActivity(mark.Id, Start, Start.AddMinutes(5));
        Results();

        var players = await Directory().ListAsync(Cancel);

        players.Count.ShouldBe(2);
        players.Single(player => player.Account == mark).Activity.ShouldBe(_activity.Kept[mark.Id]);
        players.Single(player => player.Account != mark).Activity.ShouldBeNull();
    }

    [Fact]
    public async Task Games_are_counted_by_kind_and_the_ones_won()
    {
        var mark = Add("Mark");
        var julie = Add("Julie");
        Results(
            Game(new("player1", mark.Id, null), new("player2", julie.Id, null), winner: "player1"),
            Game(new("player1", julie.Id, null), new("player2", mark.Id, null), winner: "player1", casual: true),
            Game(new("player1", mark.Id, null), new("player2", null, "greedy"), winner: "player1"),
            Game(new("player1", mark.Id, null), new("player2", null, "greedy"), winner: null));

        var player = (await Directory().FindAsync(mark.Id, Cancel)).ShouldNotBeNull();

        player.ShouldSatisfyAllConditions(
            () => player.Games.ShouldBe(4),
            () => player.Ranked.ShouldBe(1),
            () => player.AgainstPlayers.ShouldBe(2),
            () => player.AgainstBots.ShouldBe(2),
            () => player.Wins.ShouldBe(2),
            () => player.Standing.ShouldNotBeNull().Wins.ShouldBe(1));
    }

    [Fact]
    public async Task A_game_one_account_held_both_seats_of_is_one_game_and_no_win()
    {
        var mark = Add("Mark");
        Results(Game(new("player1", mark.Id, null), new("player2", mark.Id, null), winner: "player1"));

        var player = (await Directory().FindAsync(mark.Id, Cancel)).ShouldNotBeNull();

        player.Games.ShouldBe(1);
        player.Wins.ShouldBe(0);
    }

    [Fact]
    public async Task An_account_without_a_ranked_game_has_no_standing()
    {
        var mark = Add("Mark");
        Results(Game(new("player1", mark.Id, null), new("player2", null, "greedy")));

        (await Directory().FindAsync(mark.Id, Cancel)).ShouldNotBeNull().Standing.ShouldBeNull();
    }

    [Fact]
    public async Task A_host_that_keeps_no_results_lists_every_account_without_games()
    {
        Add("Mark");
        var directory = new PlayerDirectory(_accounts, new AccountActivityLog(_activity, new FixedTimeProvider(Start)), null, new FixedTimeProvider(Start));

        (await directory.ListAsync(Cancel)).ShouldHaveSingleItem().Games.ShouldBe(0);
    }

    [Fact]
    public async Task An_account_that_is_gone_is_not_found()
    {
        Results();

        (await Directory().FindAsync(AccountId.New(), Cancel)).ShouldBeNull();
    }

    [Fact]
    public void A_page_holds_twenty_players_and_says_how_many_pages_there_are()
    {
        PlayerSummary[] players = [.. Enumerable.Range(0, 45).Select(index => Summary($"player{index:00}"))];

        var page = PlayerDirectory.Page(players, PlayerSort.Name, null, 3);

        page.ShouldSatisfyAllConditions(
            () => page.Players.Select(player => player.Account.Username.Value).ShouldBe(["player40", "player41", "player42", "player43", "player44"]),
            () => page.Page.ShouldBe(3),
            () => page.Pages.ShouldBe(3),
            () => page.Total.ShouldBe(45));
    }

    [Fact]
    public void A_page_past_the_last_is_the_last()
    {
        PlayerSummary[] players = [Summary("Mark"), Summary("Julie")];

        PlayerDirectory.Page(players, PlayerSort.Name, null, 9).Page.ShouldBe(1);
    }

    [Fact]
    public void The_search_keeps_the_names_that_hold_it_whatever_the_case()
    {
        PlayerSummary[] players = [Summary("Mark"), Summary("Julie"), Summary("Marcel")];

        var page = PlayerDirectory.Page(players, PlayerSort.Name, " MAR ", 1);

        page.Players.Select(player => player.Account.Username.Value).ShouldBe(["Marcel", "Mark"]);
        page.Matching.ShouldBe(2);
        page.Total.ShouldBe(3);
    }

    [Fact]
    public void The_last_seen_come_first_by_default_and_the_never_seen_last()
    {
        PlayerSummary[] players = [Summary("Never"), Summary("Early", seen: Start), Summary("Late", seen: Start.AddHours(1))];

        PlayerDirectory.Page(players, PlayerSort.Seen, null, 1).Players.Select(player => player.Account.Username.Value).ShouldBe(["Late", "Early", "Never"]);
    }

    [Fact]
    public void Sorting_by_games_puts_the_most_first_and_ties_by_name()
    {
        PlayerSummary[] players = [Summary("Zed", games: 3), Summary("Amy", games: 3), Summary("Bob", games: 9)];

        PlayerDirectory.Page(players, PlayerSort.Games, null, 1).Players.Select(player => player.Account.Username.Value).ShouldBe(["Bob", "Amy", "Zed"]);
    }

    [Fact]
    public void Sorting_by_creation_puts_the_newest_first()
    {
        PlayerSummary[] players = [Summary("Old", created: Start.AddDays(-9)), Summary("New", created: Start)];

        PlayerDirectory.Page(players, PlayerSort.Created, null, 1).Players.Select(player => player.Account.Username.Value).ShouldBe(["New", "Old"]);
    }

    [Fact]
    public async Task A_voided_game_is_not_counted()
    {
        var mark = Add("Mark");
        Results(Game(new("player1", mark.Id, null), new("player2", null, "greedy")) with { Voided = new MatchVoid("stuck", "The match stopped answering.", "op", Start) });

        (await Directory().FindAsync(mark.Id, Cancel)).ShouldNotBeNull().Games.ShouldBe(0);
    }
}
