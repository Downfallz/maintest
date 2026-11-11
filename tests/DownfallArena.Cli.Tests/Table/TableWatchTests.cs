using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// Two processes over one store (ADR 0162): one holds a table, the other moves its journal or writes its record,
/// and the first sees it -- reads the match again when the journal moved, lets the table go when the record was
/// written -- and writes how the table ended over the record as stored, never over another's keys.
/// </summary>
public sealed class TableWatchTests : IDisposable
{
    private static readonly TableRequest Randoms = new(AgentSpec.Parse("random"), AgentSpec.Parse("random"), Who: null, Handover: null, Seed: 11);

    private readonly string _runs = Path.Combine(Path.GetTempPath(), $"downfall-watch-{Guid.NewGuid():N}");
    private readonly Owed _owedHere = new();
    private readonly Owed _owedThere = new();
    private readonly HostedTables _here;
    private readonly HostedTables _there;

    public TableWatchTests()
    {
        _here = new HostedTables(_ => new FileArtifactStore(_runs), turns: _owedHere);
        _there = new HostedTables(_ => new FileArtifactStore(_runs), index: _here.Index, turns: _owedThere);
    }

    /// <summary>
    /// Lets the hosts go, then the runs, waiting out a table that is still writing its last files: a match that ended
    /// writes its dataset after its outcome, as <see cref="HostedTables" /> waits it out too.
    /// </summary>
    public void Dispose()
    {
        _here.Dispose();
        _there.Dispose();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (Directory.Exists(_runs))
                {
                    Directory.Delete(_runs, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 50)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_table_whose_journal_another_process_moved_is_read_again()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, Randoms);
        using var elsewhere = _there.Registry();
        var picked = (await elsewhere.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        var before = table.Session.Journaled;
        (await ((IAgentTurnTaker)picked.Session).TakeTurnAsync(Cancel)).ShouldBe(AgentTurn.Taken);

        var moved = await table.Session.CatchUpAsync(Cancel);
        var again = await table.Session.CatchUpAsync(Cancel);

        moved.ShouldBeTrue();
        again.ShouldBeFalse("it stands where its journal ends now");
        table.Session.Journaled.ShouldNotBe(before);
    }

    [Fact]
    public async Task A_record_another_process_wrote_is_seen_and_one_this_process_wrote_is_not()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, HostedTables.OnePerson);
        var own = await table.Run!.RewrittenAsync(Cancel);
        using var elsewhere = _there.Registry();
        var picked = (await elsewhere.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();

        await picked.RecordAsync(Cancel);

        own.ShouldBeNull();
        (await table.Run.RewrittenAsync(Cancel)).ShouldNotBeNull().Revision.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task The_watch_lets_go_of_a_table_another_process_wrote_and_it_is_picked_up_again_as_written()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, HostedTables.OnePerson);
        using var elsewhere = _there.Registry();
        var picked = (await elsewhere.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        (await elsewhere.RekeyAsync(picked, picked.Seats[0], SeatKeys.Offered(picked.Id), Cancel)).ShouldBeTrue();
        await picked.RecordAsync(Cancel);
        var watch = new TableWatch(registry, TimeProvider.System);

        var changed = await watch.WatchOnceAsync(Cancel);

        changed.ShouldBe(1);
        registry.Held(table.Id).ShouldBeNull();
        var again = (await registry.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        again.ShouldNotBeSameAs(table);
        again.Seats[0].Token.ShouldBe(picked.Seats[0].Token);
    }

    [Fact]
    public async Task A_table_another_process_finished_is_read_to_its_end_and_kept_for_its_players()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, Randoms);
        using var elsewhere = _there.Registry();
        var picked = (IAgentTurnTaker)(await elsewhere.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull().Session;
        for (var turn = 0; turn < 2000 && await picked.TakeTurnAsync(Cancel) != AgentTurn.Done; turn++)
        {
        }

        for (var waited = 0; waited < 300 && (await _here.Stored!.RecordAsync(table.Id, Cancel))?.Status != TableRecord.Finished; waited++)
        {
            await Task.Delay(20, Cancel);
        }

        var changed = await new TableWatch(registry, TimeProvider.System).WatchOnceAsync(Cancel);

        changed.ShouldBe(1);
        registry.Held(table.Id).ShouldBeSameAs(table, "a finished table could not be picked up again: its players' seats are here");
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        table.IsOver.ShouldBeTrue();
    }

    [Fact]
    public async Task How_a_table_ended_is_written_over_the_record_as_stored_keeping_another_process_s_keys()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, HostedTables.OnePerson);
        using var elsewhere = _there.Registry();
        var picked = (await elsewhere.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        (await elsewhere.RekeyAsync(picked, picked.Seats[0], SeatKeys.Offered(picked.Id), Cancel)).ShouldBeTrue();
        await picked.RecordAsync(Cancel);

        await table.Run!.MarkAsync(TableRecord.Closed, Cancel);

        var stored = (await _here.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull();
        stored.Status.ShouldBe(TableRecord.Closed);
        stored.Seats.Single(seat => seat.Slot == "player1").Token.ShouldBe(picked.Seats[0].Token);
    }

    [Fact]
    public async Task A_table_let_go_of_is_picked_up_again_at_the_next_request()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, HostedTables.OnePerson);

        var let = registry.LetGo(table.Id);
        var again = await registry.ByIdAsync(table.Id, Cancel);

        let.ShouldBeTrue();
        again.ShouldNotBeNull().ShouldNotBeSameAs(table);
        registry.LetGo("nobody").ShouldBeFalse();
    }

    private async Task<PlayedTable> RecordedAsync(TableRegistry registry, TableRequest request)
    {
        var table = await _here.Composer.ComposeAsync(request, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        await table.RecordAsync(Cancel);
        return table;
    }

    /// <summary>Takes no turn: the tests take them, so nothing moves a journal but what a test asks for.</summary>
    private sealed class Owed : IAgentTurns
    {
        public void Owe(IAgentTurnTaker table)
        {
        }
    }
}
