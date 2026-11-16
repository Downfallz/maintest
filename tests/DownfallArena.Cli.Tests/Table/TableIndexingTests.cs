using DownfallArena.Application.Tables.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Tables;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The index of the tables follows each table's record (ADR 0161): the codes its seats have, and its summary
/// among the open tables while it is open. The record is the table; the index finds it.
/// </summary>
public sealed class TableIndexingTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry;
    private readonly string _indexDirectory = Path.Combine(Path.GetTempPath(), $"downfall-index-{Guid.NewGuid():N}");
    private readonly FileTableIndex _index;
    private readonly Failing _failing;
    private readonly TableComposer _composer;

    public TableIndexingTests()
    {
        _index = new FileTableIndex(_indexDirectory);
        _failing = new Failing(_index);
        _composer = new TableComposer(_hosted.Services, HostedTables.Rules, new FileArtifactStore(_hosted.RunsDirectory)) { Index = _failing };
        _registry = new TableRegistry(_failing);
    }

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
        if (Directory.Exists(_indexDirectory))
        {
            Directory.Delete(_indexDirectory, recursive: true);
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_recorded_table_is_listed_open_and_its_seats_codes_open_it()
    {
        var table = await Opened();

        var code = table.Seats[1].Code.ShouldNotBeNull();
        var open = (await _index.OpenAsync(0, 10, Cancel)).ShouldHaveSingleItem();

        (await _index.CodeAsync(code, Cancel)).ShouldBe(new IndexedCode(code, table.Id, "player2"));
        open.Table.ShouldBe(table.Id);
        open.Revision.ShouldBeGreaterThan(0);
        open.Summary.ShouldContain("\"slot\":\"player2\"");
    }

    [Fact]
    public async Task A_table_s_summary_holds_none_of_its_keys()
    {
        var table = await Opened();

        var summary = (await _index.OpenAsync(0, 10, Cancel)).ShouldHaveSingleItem().Summary;

        string?[] keys = [table.Pilot.Token, .. table.Seats.Select(seat => seat.Token), .. table.Seats.Select(seat => seat.Invitation), .. table.Seats.Select(seat => seat.Code)];
        foreach (var key in keys.OfType<string>())
        {
            summary.ShouldNotContain(key);
        }
    }

    [Fact]
    public async Task A_seat_offered_again_opens_by_its_new_code_and_no_more_by_its_old_one()
    {
        var table = await Opened();
        var seat = table.Seats[1];
        var before = seat.Code.ShouldNotBeNull();

        (await _registry.RekeyAsync(table, seat, SeatKeys.Offered(table.Id), Cancel)).ShouldBeTrue();
        await table.RecordAsync(Cancel);

        var after = seat.Code.ShouldNotBeNull();
        (await _index.CodeAsync(before, Cancel)).ShouldBeNull();
        (await _index.CodeAsync(after, Cancel)).ShouldBe(new IndexedCode(after, table.Id, "player2"));
    }

    /// <summary>
    /// A code a seat no longer has is taken back from the index even when the index could not be written at the
    /// moment the seat changed hands: the next write of the record takes it back.
    /// </summary>
    [Fact]
    public async Task A_code_the_index_could_not_take_back_is_taken_back_at_the_next_record_write()
    {
        var table = await Opened();
        var seat = table.Seats[1];
        var before = seat.Code.ShouldNotBeNull();
        (await _registry.RekeyAsync(table, seat, SeatKeys.Offered(table.Id), Cancel)).ShouldBeTrue();
        _failing.Fail = true;
        await table.RecordAsync(Cancel);
        var stranded = await _index.CodeAsync(before, Cancel);
        _failing.Fail = false;

        await table.RecordAsync(Cancel);

        stranded.ShouldNotBeNull("the index was out when the seat changed hands");
        (await _index.CodeAsync(before, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_table_that_ended_is_listed_no_more_and_its_codes_open_nothing()
    {
        var table = await Opened();
        var code = table.Seats[1].Code.ShouldNotBeNull();

        (await table.Api.HandleAsync("POST", "/api/seat/player1/concede", string.Empty, table.Seats[0].Token)).Status.ShouldBe(200);
        for (var attempt = 0; attempt < 500 && (await _index.OpenAsync(0, 10, Cancel)).Count > 0; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        (await _index.OpenAsync(0, 10, Cancel)).ShouldBeEmpty();
        (await _index.CodeAsync(code, Cancel)).ShouldBeNull();
    }

    /// <summary>
    /// A table whose journal no longer replays ends as a void match (ADR 0163): its record closed, it leaves the open
    /// tables of the index, and its codes with it.
    /// </summary>
    [Fact]
    public async Task A_table_voided_on_pick_up_leaves_the_index_and_its_codes_with_it()
    {
        var table = await Opened();
        var code = table.Seats[1].Code.ShouldNotBeNull();
        await PassedAsync(table);
        await DivergedAsync(table.Id);
        using var refusing = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory), basis: new ReplayBasis("abc123", "content", Application.Learning.RuleSetStamp.Of(HostedTables.Rules)));
        using var registry = await refusing.RestartedAsync();
        (await refusing.Index.OpenAsync(0, 10, Cancel)).ShouldHaveSingleItem().Table.ShouldBe(table.Id);

        var picked = await registry.ByIdAsync(table.Id, Cancel);

        picked.ShouldBeNull();
        (await registry.OpenAsync(Cancel)).ShouldBeEmpty();
        (await refusing.Index.OpenAsync(0, 10, Cancel)).ShouldBeEmpty();
        (await refusing.Index.CodeAsync(code, Cancel)).ShouldBeNull();
    }

    /// <summary>Both seats pass their first evolution, which writes two lines into the journal.</summary>
    private static async Task PassedAsync(PlayedTable table)
    {
        foreach (var seat in table.Seats)
        {
            var asked = await AskedAsync(seat.Person!);
            var pass = System.Text.Json.JsonSerializer.Serialize(new { kind = "Evolution", pass = true, asked });
            (await table.Api.HandleAsync("POST", $"/api/seat/{seat.Name}/decision", pass, seat.Token)).Status.ShouldBe(204);
        }
    }

    private static async Task<long> AskedAsync(ISeatPerson person)
    {
        for (var attempt = 0; attempt < 300 && person.Waiting is null; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        return person.Waiting.ShouldNotBeNull().Asked;
    }

    /// <summary>The journal's second line handed to the other seat: a line that no longer answers the question asked.</summary>
    private async Task DivergedAsync(string id)
    {
        var path = Path.Combine(_hosted.RunsDirectory, id, PlaytestRun.JournalFile);
        for (var attempt = 0; attempt < 300 && (File.Exists(path) ? File.ReadAllLines(path).Length : 0) < 2; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        var lines = (await File.ReadAllLinesAsync(path, Cancel)).Select(line => System.Text.Json.Nodes.JsonNode.Parse(line)!.AsObject()).ToList();
        lines[1]["slot"] = (string?)lines[1]["slot"] == "player1" ? "player2" : "player1";
        await File.WriteAllLinesAsync(path, lines.Select(line => line.ToJsonString()), Cancel);
    }

    /// <summary>Two people, both arrived, so the match has begun and a seat can give it up.</summary>
    private async Task<PlayedTable> Opened()
    {
        var table = await _composer.ComposeAsync(HostedTables.TwoPeople, Cancel);
        using (var place = (await _registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await _registry.AddAsync(table, place, Cancel);
        }

        await table.RecordAsync(Cancel);
        foreach (var seat in table.Seats)
        {
            (await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token)).Status.ShouldBe(200);
        }

        await table.RecordAsync(Cancel);
        table.Session.HasBegun.ShouldBeTrue();
        return table;
    }

    /// <summary>The index, which can be made to refuse every write: a store that is out.</summary>
    private sealed class Failing(ITableIndex inner) : ITableIndex
    {
        public bool Fail { get; set; }

        public Task<IndexedCode?> CodeAsync(string code, CancellationToken cancellationToken = default) => inner.CodeAsync(code, cancellationToken);

        public Task<bool> AddCodeAsync(IndexedCode code, CancellationToken cancellationToken = default) => Fail ? Out<bool>() : inner.AddCodeAsync(code, cancellationToken);

        public Task RemoveCodeAsync(string code, string table, CancellationToken cancellationToken = default) => Fail ? Out<bool>() : inner.RemoveCodeAsync(code, table, cancellationToken);

        public Task PutOpenAsync(IndexedTable table, CancellationToken cancellationToken = default) => Fail ? Out<bool>() : inner.PutOpenAsync(table, cancellationToken);

        public Task CloseAsync(string table, DateTimeOffset createdAt, long revision, CancellationToken cancellationToken = default) => Fail ? Out<bool>() : inner.CloseAsync(table, createdAt, revision, cancellationToken);

        public Task<IReadOnlyList<IndexedTable>> OpenAsync(int skip, int take, CancellationToken cancellationToken = default) => inner.OpenAsync(skip, take, cancellationToken);

        private static Task<TResult> Out<TResult>() => Task.FromException<TResult>(new IOException("The index is out."));
    }
}
