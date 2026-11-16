using DownfallArena.Application.Tables.Ports;
using DownfallArena.Application.Tables.Tending;

namespace DownfallArena.Infrastructure.Tests.Tables;

/// <summary>What every tend log promises, played against each adapter.</summary>
public abstract class TendLogContract
{
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 18, 0, 0, TimeSpan.Zero);

    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<ITendLog> LogAsync();

    private static TendRun Run(DateTimeOffset started, TendOutcome outcome = TendOutcome.Completed) =>
        new(TendRun.NewId(started), started, started.AddSeconds(3), TendTrigger.Manual, "mk", outcome, outcome == TendOutcome.Failed ? "the store is out" : null,
            ["never-began"], 2, ["timed-out"], ["abandoned"], ["warned"]);

    [Fact]
    public async Task A_run_reads_back_as_it_was_recorded()
    {
        var log = await LogAsync();
        var run = Run(Start, TendOutcome.Failed);

        await log.RecordAsync(run, Cancel);

        var read = (await log.FindAsync(run.Id, Cancel)).ShouldNotBeNull();
        read.ShouldSatisfyAllConditions(
            () => read.Started.ShouldBe(run.Started),
            () => read.Finished.ShouldBe(run.Finished),
            () => read.Trigger.ShouldBe(TendTrigger.Manual),
            () => read.By.ShouldBe("mk"),
            () => read.Outcome.ShouldBe(TendOutcome.Failed),
            () => read.Error.ShouldBe("the store is out"),
            () => read.NeverBegan.ShouldBe(["never-began"]),
            () => read.InvitationsExpired.ShouldBe(2),
            () => read.TimedOut.ShouldBe(["timed-out"]),
            () => read.Abandoned.ShouldBe(["abandoned"]),
            () => read.Warned.ShouldBe(["warned"]));
    }

    [Fact]
    public async Task Ids_are_listed_newest_first()
    {
        var log = await LogAsync();
        TendRun[] runs = [Run(Start.AddHours(1)), Run(Start), Run(Start.AddHours(2))];
        foreach (var run in runs)
        {
            await log.RecordAsync(run, Cancel);
        }

        (await log.IdsAsync(Cancel)).ShouldBe([runs[2].Id, runs[0].Id, runs[1].Id]);
    }

    [Fact]
    public async Task A_log_with_nothing_kept_lists_nothing_and_finds_nothing()
    {
        var log = await LogAsync();

        (await log.IdsAsync(Cancel)).ShouldBeEmpty();
        (await log.FindAsync(TendRun.NewId(Start), Cancel)).ShouldBeNull();
        (await log.FindAsync("../accounts/session.key", Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task Past_the_kept_runs_the_oldest_go()
    {
        var log = await LogAsync();
        for (var hour = 0; hour < TendRun.Kept + 2; hour++)
        {
            await log.RecordAsync(Run(Start.AddHours(hour)), Cancel);
        }

        var ids = await log.IdsAsync(Cancel);

        ids.Count.ShouldBe(TendRun.Kept);
        TendRun.StartedOf(ids[^1]).ShouldBe(Start.AddHours(2));
    }
}
