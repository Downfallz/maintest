using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>What every results store promises, played against each adapter.</summary>
public abstract class MatchResultsContract
{
    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<IMatchResults> ResultsAsync();

    protected static MatchResult Finished(DateTimeOffset at, string? winner = "player1") =>
        new(Guid.CreateVersion7().ToString(), at, "2v2", "at-activation", "f3e8a42d", 11, winner, [new ResultSeat("player1", AccountId.New(), null), new ResultSeat("player2", null, "greedy")]);

    [Fact]
    public async Task A_result_reads_back_as_it_was_recorded()
    {
        var results = await ResultsAsync();
        var result = Finished(new DateTimeOffset(2026, 11, 3, 20, 0, 0, TimeSpan.Zero), winner: null);

        await results.RecordAsync(result, Cancel);

        var read = (await results.ListAsync(Cancel)).ShouldHaveSingleItem();
        read.MatchId.ShouldBe(result.MatchId);
        read.FinishedAt.ShouldBe(result.FinishedAt);
        read.Winner.ShouldBeNull();
        read.Rounds.ShouldBe(11);
        read.Seats.ShouldBe(result.Seats);
    }

    [Fact]
    public async Task Results_are_listed_oldest_first_across_months_and_a_match_recorded_twice_is_kept_once()
    {
        var results = await ResultsAsync();
        var december = Finished(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));
        var november = Finished(new DateTimeOffset(2026, 11, 30, 23, 0, 0, TimeSpan.Zero));

        await results.RecordAsync(december, Cancel);
        await results.RecordAsync(november, Cancel);
        await results.RecordAsync(december, Cancel);

        (await results.ListAsync(Cancel)).Select(result => result.MatchId).ShouldBe([november.MatchId, december.MatchId]);
    }

    [Fact]
    public async Task A_result_keeps_the_table_it_was_played_at_and_one_kept_before_that_reads_without_it()
    {
        var results = await ResultsAsync();
        var at = new DateTimeOffset(2026, 11, 3, 20, 0, 0, TimeSpan.Zero);
        var played = Finished(at) with { Table = "table-1" };
        var older = Finished(at.AddMinutes(1));

        await results.RecordAsync(played, Cancel);
        await results.RecordAsync(older, Cancel);

        (await results.ListAsync(Cancel)).Select(result => result.Table).ShouldBe(["table-1", null]);
    }

    [Fact]
    public async Task A_result_kept_as_unranked_reads_back_unranked()
    {
        var results = await ResultsAsync();
        var casual = Finished(new DateTimeOffset(2026, 11, 9, 20, 0, 0, TimeSpan.Zero)) with { Casual = true };

        await results.RecordAsync(casual, Cancel);

        (await results.ListAsync(Cancel)).ShouldHaveSingleItem().Casual.ShouldBeTrue();
    }

    [Fact]
    public async Task An_account_s_results_are_the_matches_it_held_a_seat_in_oldest_first()
    {
        var results = await ResultsAsync();
        var at = new DateTimeOffset(2026, 11, 3, 20, 0, 0, TimeSpan.Zero);
        var me = AccountId.New();
        var later = Finished(at.AddDays(1)) with { Seats = [new ResultSeat("player1", AccountId.New(), null), new ResultSeat("player2", me, null)] };
        var earlier = Finished(at) with { Seats = [new ResultSeat("player1", me, null), new ResultSeat("player2", null, "greedy")] };
        var others = Finished(at.AddHours(1));

        await results.RecordAsync(later, Cancel);
        await results.RecordAsync(others, Cancel);
        await results.RecordAsync(earlier, Cancel);

        (await results.ListForAsync(me, Cancel)).Select(result => result.MatchId).ShouldBe([earlier.MatchId, later.MatchId]);
    }

    [Fact]
    public async Task A_match_whose_two_seats_one_account_held_is_listed_once_for_it()
    {
        var results = await ResultsAsync();
        var me = AccountId.New();
        var shared = Finished(new DateTimeOffset(2026, 11, 3, 20, 0, 0, TimeSpan.Zero)) with { Seats = [new ResultSeat("player1", me, null), new ResultSeat("player2", me, null)] };

        await results.RecordAsync(shared, Cancel);

        (await results.ListForAsync(me, Cancel)).ShouldHaveSingleItem().MatchId.ShouldBe(shared.MatchId);
    }

    [Fact]
    public async Task An_account_that_played_nothing_has_no_results()
    {
        var results = await ResultsAsync();
        await results.RecordAsync(Finished(new DateTimeOffset(2026, 11, 3, 20, 0, 0, TimeSpan.Zero)), Cancel);

        (await results.ListForAsync(AccountId.New(), Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_store_nothing_was_recorded_in_lists_nothing()
    {
        (await (await ResultsAsync()).ListAsync(Cancel)).ShouldBeEmpty();
    }
}
