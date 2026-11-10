using DownfallArena.Application.Tables.Ports;

namespace DownfallArena.Infrastructure.Tests.Tables;

/// <summary>
/// What every index of the tables does (ADR 0161), whatever it is kept in: a code opens one seat of one table,
/// and the lobby reads the open tables newest first, never an older summary over a newer one.
/// </summary>
public abstract class TableIndexContract
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A fresh, empty index.</summary>
    protected abstract Task<ITableIndex> IndexAsync();

    [Fact]
    public async Task A_code_opens_the_seat_it_was_given_to_and_nobody_else_can_take_it()
    {
        var index = await IndexAsync();

        var given = await index.AddCodeAsync(new IndexedCode("ABCD1234", "table-1", "player2"), TestContext.Current.CancellationToken);
        var again = await index.AddCodeAsync(new IndexedCode("ABCD1234", "table-2", "player1"), TestContext.Current.CancellationToken);

        given.ShouldBeTrue();
        again.ShouldBeFalse();
        (await index.CodeAsync("ABCD1234", TestContext.Current.CancellationToken)).ShouldBe(new IndexedCode("ABCD1234", "table-1", "player2"));
        (await index.CodeAsync("ZZZZ9999", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task A_code_is_taken_back_only_by_the_table_it_opens()
    {
        var index = await IndexAsync();
        await index.AddCodeAsync(new IndexedCode("ABCD1234", "table-1", "player2"), TestContext.Current.CancellationToken);

        await index.RemoveCodeAsync("ABCD1234", "table-2", TestContext.Current.CancellationToken);
        var kept = await index.CodeAsync("ABCD1234", TestContext.Current.CancellationToken);
        await index.RemoveCodeAsync("ABCD1234", "table-1", TestContext.Current.CancellationToken);

        kept.ShouldNotBeNull();
        (await index.CodeAsync("ABCD1234", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await index.AddCodeAsync(new IndexedCode("ABCD1234", "table-3", "player1"), TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task The_open_tables_are_listed_newest_first_a_page_at_a_time()
    {
        var index = await IndexAsync();
        foreach (var minutes in new[] { 1, 3, 2 })
        {
            await index.PutOpenAsync(new IndexedTable($"table-{minutes}", Noon.AddMinutes(minutes), 1, "{}"), TestContext.Current.CancellationToken);
        }

        var first = await index.OpenAsync(skip: 0, take: 2, TestContext.Current.CancellationToken);
        var second = await index.OpenAsync(skip: 2, take: 2, TestContext.Current.CancellationToken);

        first.Select(table => table.Table).ShouldBe(["table-3", "table-2"]);
        second.Select(table => table.Table).ShouldBe(["table-1"]);
    }

    [Fact]
    public async Task An_older_summary_of_a_table_does_not_replace_a_newer_one()
    {
        var index = await IndexAsync();

        await index.PutOpenAsync(new IndexedTable("table-1", Noon, 3, "{\"seats\":1}"), TestContext.Current.CancellationToken);
        await index.PutOpenAsync(new IndexedTable("table-1", Noon, 2, "{\"seats\":2}"), TestContext.Current.CancellationToken);
        var kept = (await index.OpenAsync(0, 10, TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
        await index.PutOpenAsync(new IndexedTable("table-1", Noon, 4, "{\"seats\":0}"), TestContext.Current.CancellationToken);

        kept.Revision.ShouldBe(3);
        kept.Summary.ShouldBe("{\"seats\":1}");
        (await index.OpenAsync(0, 10, TestContext.Current.CancellationToken)).ShouldHaveSingleItem().Summary.ShouldBe("{\"seats\":0}");
    }

    [Fact]
    public async Task A_table_taken_out_of_the_open_ones_is_listed_no_more()
    {
        var index = await IndexAsync();
        await index.PutOpenAsync(new IndexedTable("table-1", Noon, 1, "{}"), TestContext.Current.CancellationToken);
        await index.PutOpenAsync(new IndexedTable("table-2", Noon.AddMinutes(1), 1, "{}"), TestContext.Current.CancellationToken);

        await index.CloseAsync("table-1", Noon, 2, TestContext.Current.CancellationToken);
        await index.CloseAsync("table-9", Noon, 1, TestContext.Current.CancellationToken);

        (await index.OpenAsync(0, 10, TestContext.Current.CancellationToken)).Select(table => table.Table).ShouldBe(["table-2"]);
    }

    /// <summary>
    /// Another host can write a table's summary late, after the table closed: the index keeps the revision it
    /// closed at, and an earlier one does not list the table again.
    /// </summary>
    [Fact]
    public async Task A_summary_of_an_earlier_revision_written_after_the_table_closed_does_not_list_it_again()
    {
        var index = await IndexAsync();
        await index.PutOpenAsync(new IndexedTable("table-1", Noon, 1, "{}"), TestContext.Current.CancellationToken);

        await index.CloseAsync("table-1", Noon, 3, TestContext.Current.CancellationToken);
        await index.PutOpenAsync(new IndexedTable("table-1", Noon, 2, "{}"), TestContext.Current.CancellationToken);

        (await index.OpenAsync(0, 10, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }
}
