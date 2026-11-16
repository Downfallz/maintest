using DownfallArena.Application.Tables.Tending;

namespace DownfallArena.Application.Tests.Tables.Tending;

public sealed class TendRunTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 18, 0, 0, 123, TimeSpan.Zero);

    internal static TendRun Run(DateTimeOffset started, TendOutcome outcome = TendOutcome.Completed, string[]? neverBegan = null, int expired = 0, string[]? timedOut = null, string[]? abandoned = null, string[]? warned = null) =>
        new(TendRun.NewId(started), started, started.AddSeconds(4), TendTrigger.Schedule, null, outcome, outcome == TendOutcome.Failed ? "the store is out" : null,
            neverBegan ?? [], expired, timedOut ?? [], abandoned ?? [], warned ?? []);

    [Fact]
    public void A_run_id_starts_with_when_it_started_to_the_millisecond()
    {
        var id = TendRun.NewId(Start);

        id.ShouldStartWith("20261113T180000123Z-");
        TendRun.StartedOf(id).ShouldBe(Start);
        TendRun.IsId(id).ShouldBeTrue();
    }

    [Fact]
    public void Ids_sort_as_their_runs_started()
    {
        string[] ids = [TendRun.NewId(Start.AddHours(1)), TendRun.NewId(Start), TendRun.NewId(Start.AddDays(1))];

        ids.Order(StringComparer.Ordinal).Select(TendRun.StartedOf).ShouldBe([Start, Start.AddHours(1), Start.AddDays(1)]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../accounts/x")]
    [InlineData("20261113T180000123Z")]
    [InlineData("20261113T180000123Z-ABCDEF")]
    [InlineData("20261313T180000123Z-abcdef")]
    public void A_name_no_run_would_have_is_not_an_id(string? id)
    {
        TendRun.IsId(id).ShouldBeFalse();
    }

    [Fact]
    public void Totals_add_up_the_runs_started_since_then_and_count_the_failed_ones()
    {
        TendRun[] runs =
        [
            Run(Start, neverBegan: ["a", "b"], expired: 1, timedOut: ["c"], warned: ["d"]),
            Run(Start.AddHours(1), TendOutcome.Failed),
            Run(Start.AddHours(2), abandoned: ["e"], expired: 2),
            Run(Start.AddHours(-30), neverBegan: ["old"]),
        ];

        TendTotals.Since(runs, Start).ShouldBe(new TendTotals(Runs: 3, Failed: 1, NeverBegan: 2, InvitationsExpired: 3, TimedOut: 1, Abandoned: 1, Warned: 1));
    }

    [Fact]
    public void No_run_adds_up_to_nothing()
    {
        TendTotals.Since([], Start).ShouldBe(TendTotals.None);
    }
}
