using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A table's record is written over the one stored (ADR 0159): another host may hold the same table, and what it
/// wrote that this host does not know of yet is not undone.
/// </summary>
public sealed class TableRecordTests
{
    [Fact]
    public void A_record_written_over_a_table_that_ended_keeps_it_ended()
    {
        var written = Record(TableRecord.Open).Over(Record(TableRecord.Finished));

        written.Status.ShouldBe(TableRecord.Finished);
    }

    [Fact]
    public void A_record_written_over_one_that_says_the_match_began_keeps_saying_it()
    {
        var written = Record(TableRecord.Open).Over(Record(TableRecord.Open) with { Begun = true });

        written.Begun.ShouldBeTrue();
    }

    [Fact]
    public void A_record_written_over_a_refused_one_keeps_the_refusal()
    {
        var basis = new ReplayBasis("engine", "content", DownfallArena.Application.Learning.RuleSetStamp.Of(HostedTables.Rules));

        var written = Record(TableRecord.Open).Over(Record(TableRecord.Open) with { RefusedUnder = basis });

        written.RefusedUnder.ShouldBe(basis);
    }

    [Fact]
    public void Each_record_written_is_the_next_revision_and_the_first_is_one()
    {
        var first = Record(TableRecord.Open).Over(stored: null);
        var second = Record(TableRecord.Open).Over(first);

        first.Revision.ShouldBe(1);
        second.Revision.ShouldBe(2);
    }

    [Fact]
    public void A_table_that_ended_here_says_so_over_a_record_that_does_not()
    {
        var written = Record(TableRecord.Closed).Over(Record(TableRecord.Open));

        written.Status.ShouldBe(TableRecord.Closed);
    }

    private static TableRecord Record(string status) => new()
    {
        Id = "20261110-120000-aaaaaaaa",
        CreatedAt = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero),
        Seed = 7,
        Match = Guid.Empty.ToString(),
        Seats = [],
        PilotToken = "pilot",
        Status = status,
    };
}
