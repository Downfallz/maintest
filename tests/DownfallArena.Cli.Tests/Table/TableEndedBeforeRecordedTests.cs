using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A match of two bots can be over before its table is first written down: its end is not lost then, or the
/// table would be open in its record and in the index (ADR 0161) for good, taking a place and picked up again.
/// </summary>
public sealed class TableEndedBeforeRecordedTests
{
    [Fact]
    public async Task A_recorded_table_whose_match_ended_before_its_first_record_is_written_down_as_finished()
    {
        using var hosted = new HostedTables(recording: true);
        using var registry = hosted.Registry();
        var table = await EndedAsync(hosted);

        using var place = await registry.ReserveAsync(TestContext.Current.CancellationToken);
        await registry.AddAsync(table, place.ShouldNotBeNull(), TestContext.Current.CancellationToken);
        await table.RecordAsync(TestContext.Current.CancellationToken);

        (await hosted.Stored!.RecordAsync(table.Id, TestContext.Current.CancellationToken)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Finished);
        (await hosted.Index.OpenAsync(0, 10, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_table_that_records_nothing_and_ended_before_its_first_record_is_not_listed_open()
    {
        using var hosted = new HostedTables();
        using var registry = hosted.Registry();
        var table = await EndedAsync(hosted);

        using var place = await registry.ReserveAsync(TestContext.Current.CancellationToken);
        await registry.AddAsync(table, place.ShouldNotBeNull(), TestContext.Current.CancellationToken);
        await table.RecordAsync(TestContext.Current.CancellationToken);

        // The end of a table that records nothing is marked as its outcome is observed, which may be just before
        // the first record or just after it: either way the index ends up saying it is not open.
        var listed = await hosted.Index.OpenAsync(0, 10, TestContext.Current.CancellationToken);
        for (var waited = 0; listed.Count > 0 && waited < 100; waited++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
            listed = await hosted.Index.OpenAsync(0, 10, TestContext.Current.CancellationToken);
        }

        listed.ShouldBeEmpty();
    }

    /// <summary>A table of two bots, composed and played to its end, its closing over, before anything writes it down.</summary>
    private static async Task<PlayedTable> EndedAsync(HostedTables hosted)
    {
        var table = await hosted.Composer.ComposeAsync(HostedTables.Bots, TestContext.Current.CancellationToken);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        for (var waited = 0; !table.IsFinished && waited < 300; waited++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        table.IsFinished.ShouldBeTrue();
        return table;
    }
}
