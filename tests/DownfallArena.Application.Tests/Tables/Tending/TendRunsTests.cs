using DownfallArena.Application.Tables.Ports;
using DownfallArena.Application.Tables.Tending;
using NSubstitute;

namespace DownfallArena.Application.Tests.Tables.Tending;

public sealed class TendRunsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 18, 30, 0, TimeSpan.Zero);

    private readonly ITendLog _log = Substitute.For<ITendLog>();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private void Keep(params TendRun[] runs)
    {
        _log.IdsAsync(Arg.Any<CancellationToken>()).Returns([.. runs.Select(run => run.Id).OrderDescending(StringComparer.Ordinal)]);
        foreach (var run in runs)
        {
            _log.FindAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);
        }
    }

    [Fact]
    public async Task A_page_holds_twenty_runs_the_newest_first()
    {
        Keep([.. Enumerable.Range(0, 45).Select(hours => TendRunTests.Run(Now.AddHours(-hours)))]);

        var page = await new TendRuns(_log).PageAsync(1, Now, Cancel);

        page.Runs.Count.ShouldBe(20);
        page.Runs[0].Started.ShouldBe(Now);
        page.Total.ShouldBe(45);
        page.Pages.ShouldBe(3);
    }

    [Fact]
    public async Task A_page_past_the_last_is_the_last()
    {
        Keep(TendRunTests.Run(Now));

        (await new TendRuns(_log).PageAsync(7, Now, Cancel)).Page.ShouldBe(1);
    }

    [Fact]
    public async Task The_totals_are_the_last_day_and_the_last_week()
    {
        Keep(TendRunTests.Run(Now.AddHours(-1), timedOut: ["a"]), TendRunTests.Run(Now.AddDays(-3), timedOut: ["b"]), TendRunTests.Run(Now.AddDays(-9), timedOut: ["c"]));

        var page = await new TendRuns(_log).PageAsync(1, Now, Cancel);

        page.Day.TimedOut.ShouldBe(1);
        page.Week.TimedOut.ShouldBe(2);
        page.Week.Runs.ShouldBe(2);
    }

    [Fact]
    public async Task The_totals_read_no_run_older_than_a_week()
    {
        var old = TendRunTests.Run(Now.AddDays(-30));
        Keep([old, .. Enumerable.Range(0, 25).Select(hours => TendRunTests.Run(Now.AddHours(-hours)))]);

        await new TendRuns(_log).PageAsync(1, Now, Cancel);

        await _log.DidNotReceive().FindAsync(old.Id, Arg.Any<CancellationToken>());
    }
}
