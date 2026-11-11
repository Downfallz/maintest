using DownfallArena.Application.Tables.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Tables;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The index of the tables (ADR 0161) finds and lists; the records are the tables. So an index that does not
/// answer fails no request it can be read past, leaves no table half added, and an index that is behind its
/// records is put back in line with them.
/// </summary>
public sealed class TableIndexFailuresTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly Failing _failing;

    public TableIndexFailuresTests()
    {
        _failing = new Failing(_hosted.Index);
    }

    public void Dispose() => _hosted.Dispose();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_table_whose_codes_cannot_be_taken_is_not_added_and_its_place_is_given_back()
    {
        using var registry = new TableRegistry(_failing, mostUnderWay: 1);
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull();
        _failing.Fail = true;

        await Should.ThrowAsync<IOException>(() => registry.AddAsync(table, place, Cancel));
        place.Dispose();
        table.Dispose();
        _failing.Fail = false;

        registry.HeldTables().ShouldBeEmpty();
        (await registry.ReserveAsync(Cancel)).ShouldNotBeNull().Dispose();
    }

    [Fact]
    public async Task The_sweep_goes_on_when_the_index_does_not_answer()
    {
        using var registry = new TableRegistry(_failing) { Loader = new TableLoader(_hosted.Stored!, _hosted.Composer, TimeProvider.System) };
        _failing.Fail = true;

        var swept = await registry.SweepAsync(DateTimeOffset.UtcNow.AddDays(2), Cancel);

        swept.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_tables_held_here_are_still_listed_and_counted_when_the_index_does_not_answer()
    {
        using var registry = new TableRegistry(_failing, mostUnderWay: 1);
        var table = await AddedAsync(registry);
        _failing.Fail = true;

        var listed = await registry.OpenAsync(Cancel);
        var place = await registry.ReserveAsync(Cancel);

        listed.ShouldHaveSingleItem().Id.ShouldBe(table.Id);
        place.ShouldBeNull("the table held here takes the one place");
    }

    [Fact]
    public async Task Keys_put_back_after_a_save_that_failed_need_nothing_of_the_index()
    {
        using var registry = new TableRegistry(_failing);
        var table = await AddedAsync(registry);
        var seat = table.Seats[0];
        var previous = registry.KeysOf(seat);
        (await registry.RekeyAsync(table, seat, SeatKeys.Offered(table.Id), Cancel)).ShouldBeTrue();
        _failing.Fail = true;

        var restored = registry.Restore(table, seat, previous);

        restored.ShouldBeTrue();
        seat.Token.ShouldBe(previous.Token);
        seat.Code.ShouldBe(previous.Code);
        (await registry.ByTokenAsync(previous.Token, Cancel)).ShouldBeSameAs(table);
    }

    [Fact]
    public async Task A_table_the_index_lists_whose_record_ended_is_taken_out_of_the_index_by_the_start_up_pass()
    {
        var ended = await EndedAsync();
        using var later = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory));
        await later.Index.PutOpenAsync(new IndexedTable(ended.Id, ended.CreatedAt, 1, TableSummary.Of(ended).ToJson()), Cancel);

        using var registry = await later.RestartedAsync();

        (await later.Index.OpenAsync(0, 10, Cancel)).ShouldBeEmpty();
        (await registry.ReserveAsync(Cancel)).ShouldNotBeNull().Dispose();
    }

    [Fact]
    public async Task A_table_the_index_lists_whose_record_ended_is_taken_out_of_the_index_when_it_is_asked_for()
    {
        var ended = await EndedAsync();
        using var later = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory));
        await later.Index.PutOpenAsync(new IndexedTable(ended.Id, ended.CreatedAt, 1, TableSummary.Of(ended).ToJson()), Cancel);
        using var registry = later.Registry();

        var picked = await registry.ByIdAsync(ended.Id, Cancel);

        picked.ShouldBeNull();
        (await later.Index.OpenAsync(0, 10, Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_table_opened_from_the_lobby_takes_no_code_for_the_seats_it_hands_out()
    {
        var counting = new Failing(_hosted.Index);
        using var registry = new TableRegistry(counting) { Loader = new TableLoader(_hosted.Stored!, _hosted.Composer, TimeProvider.System) };
        var lobby = new LobbyApi(registry, _hosted.Composer, TimeProvider.System);

        var opened = await lobby.HandleAsync("POST", "/api/lobby/tables", """{"opponent":"hotseat","format":"1v1"}""");

        opened.Status.ShouldBe(201);
        counting.CodesTaken.ShouldBe(0, "both seats are this browser's, and no link or code names either");
    }

    /// <summary>A table of one person against a bot, added and written down.</summary>
    private async Task<PlayedTable> AddedAsync(TableRegistry registry)
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        using var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull();
        await registry.AddAsync(table, place, Cancel);
        await table.RecordAsync(Cancel);
        return table;
    }

    /// <summary>The record of a table of two bots, played to its end and written down as finished.</summary>
    private async Task<TableRecord> EndedAsync()
    {
        using var registry = _hosted.Registry();
        var table = await _hosted.Composer.ComposeAsync(HostedTables.Bots, Cancel);
        using var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull();
        await registry.AddAsync(table, place, Cancel);
        await table.RecordAsync(Cancel);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), Cancel);
        for (var waited = 0; !table.IsFinished && waited < 300; waited++)
        {
            await Task.Delay(100, Cancel);
        }

        var record = (await _hosted.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull();
        record.Status.ShouldBe(TableRecord.Finished);
        return record;
    }

    /// <summary>An index that stops answering when told to, and counts the codes taken in it.</summary>
    private sealed class Failing(ITableIndex inner) : ITableIndex
    {
        private int _codesTaken;

        public bool Fail { get; set; }

        public int CodesTaken => Volatile.Read(ref _codesTaken);

        public Task<IndexedCode?> CodeAsync(string code, CancellationToken cancellationToken = default) => Fail ? Out<IndexedCode?>() : inner.CodeAsync(code, cancellationToken);

        public async Task<bool> AddCodeAsync(IndexedCode code, CancellationToken cancellationToken = default)
        {
            if (Fail)
            {
                return await Out<bool>();
            }

            var taken = await inner.AddCodeAsync(code, cancellationToken);
            if (taken)
            {
                Interlocked.Increment(ref _codesTaken);
            }

            return taken;
        }

        public Task RemoveCodeAsync(string code, string table, CancellationToken cancellationToken = default) => Fail ? Out<bool>() : inner.RemoveCodeAsync(code, table, cancellationToken);

        public Task PutOpenAsync(IndexedTable table, CancellationToken cancellationToken = default) => Fail ? Out<bool>() : inner.PutOpenAsync(table, cancellationToken);

        public Task CloseAsync(string table, DateTimeOffset createdAt, long revision, CancellationToken cancellationToken = default) => Fail ? Out<bool>() : inner.CloseAsync(table, createdAt, revision, cancellationToken);

        public Task<IReadOnlyList<IndexedTable>> OpenAsync(int skip, int take, CancellationToken cancellationToken = default) => Fail ? Out<IReadOnlyList<IndexedTable>>() : inner.OpenAsync(skip, take, cancellationToken);

        private static Task<TResult> Out<TResult>() => Task.FromException<TResult>(new IOException("The index is out."));
    }
}
