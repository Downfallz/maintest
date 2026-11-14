using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Players;
using DownfallArena.Application.Admin;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Admin;

/// <summary>The players and the finished games at a glance (headquarters A5), counted over the last day and week.</summary>
public sealed class OverviewCountsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 3, 0, 0, TimeSpan.Zero);

    private static PlayerSummary Player(int madeDaysAgo, int? seenDaysAgo, bool suspended = false)
    {
        var account = new Account(AccountId.New(), new ExternalIdentity(ExternalIdentity.Google, Guid.NewGuid().ToString()), Username.Create("Somebody").Value, Now.AddDays(-madeDaysAgo), 1)
        {
            Suspension = suspended ? new AccountSuspension("Spamming invitations to strangers.", "op", Now) : null,
        };
        var activity = seenDaysAgo is { } days ? new AccountActivity(account.Id, null, Now.AddDays(-days)) : null;
        return new PlayerSummary(account, activity, 0, 0, 0, 0, 0, null);
    }

    private static MatchResult Result(double hoursAgo, bool voided = false) =>
        new(Guid.CreateVersion7().ToString(), Now.AddHours(-hoursAgo), "3v3", "at-activation", "hash", 9, "player1",
            [new ResultSeat("player1", AccountId.New(), null), new ResultSeat("player2", null, "greedy")])
        {
            Voided = voided ? MatchVoid.Create("engine", "The engine resolved one spell twice.", "op", Now).Value : null,
        };

    [Fact]
    public void Players_are_counted_with_those_seen_and_made_this_week_and_those_suspended()
    {
        var counts = PlayerCounts.Of([Player(30, 1), Player(3, 6), Player(10, 8), Player(2, null, suspended: true)], Now);

        counts.ShouldBe(new PlayerCounts(Total: 4, SeenThisWeek: 2, NewThisWeek: 2, Suspended: 1));
    }

    [Fact]
    public void Finished_games_count_the_last_day_and_week_and_a_voided_one_only_as_voided()
    {
        var counts = FinishedCounts.Of([Result(2), Result(30), Result(24 * 6), Result(24 * 9), Result(5, voided: true)], Now);

        counts.ShouldBe(new FinishedCounts(LastDay: 1, LastWeek: 3, VoidedThisWeek: 1));
    }

    [Fact]
    public void A_host_with_no_player_and_no_game_counts_nothing()
    {
        PlayerCounts.Of([], Now).ShouldBe(new PlayerCounts(0, 0, 0, 0));
        FinishedCounts.Of([], Now).ShouldBe(new FinishedCounts(0, 0, 0));
    }
}
