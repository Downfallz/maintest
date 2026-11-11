using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Learning.Recording;
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
    public async Task A_match_two_processes_hold_is_written_by_one_of_them_and_served_by_both()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, Randoms);
        using var elsewhere = _there.Registry();
        var there = (await elsewhere.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        await PlayedOutAsync((IAgentTurnTaker)there.Session);

        (await table.Session.CatchUpAsync(Cancel)).ShouldBeTrue();
        await ClosedAsync(table);
        await ClosedAsync(there);

        var episodes = await File.ReadAllLinesAsync(Path.Combine(_runs, table.Id, RunRecorder.EpisodesFile), Cancel);
        episodes.Length.ShouldBe(2, "one episode a seat: the match is written once, not once by each process");
        table.Run!.IsClosed.ShouldBeTrue("the session is served where it was not written, once it is");
    }

    [Fact]
    public async Task A_run_that_takes_over_a_claim_that_ran_out_writes_the_match_once()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, Randoms);
        using var elsewhere = _there.Registry();
        var there = (await elsewhere.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        await PlayedOutAsync((IAgentTurnTaker)there.Session);
        await ClosedAsync(there);
        // The other process wrote the match and stopped before its record said so: its claim ran out since.
        var record = (await _here.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull();
        await new FileArtifactStore(_runs).Writer(table.Id).WriteJsonAsync(TableRecord.File, record with { Status = TableRecord.Open }, Cancel);
        await File.WriteAllTextAsync(
            Path.Combine(_runs, table.Id, PlaytestRun.ClosingFile),
            JsonSerializer.Serialize(new { By = "stopped", At = DateTimeOffset.UtcNow - PlaytestRun.ClosingHeld - TimeSpan.FromMinutes(1) }, ArtifactJson.DocumentOptions),
            Cancel);

        (await table.Session.CatchUpAsync(Cancel)).ShouldBeTrue();
        await ClosedAsync(table);

        var episodes = await File.ReadAllLinesAsync(Path.Combine(_runs, table.Id, RunRecorder.EpisodesFile), Cancel);
        episodes.Length.ShouldBe(2, "the dataset is started over by whoever closes it, not added to");
        (await _here.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Finished);
    }

    [Fact]
    public async Task A_claim_on_closing_a_session_stands_for_one_run_until_it_runs_out()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, HostedTables.OnePerson);
        using var elsewhere = _there.Registry();
        var there = (await elsewhere.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();

        var first = await table.Run!.ClaimClosingAsync(Cancel);
        var second = await there.Run!.ClaimClosingAsync(Cancel);
        var again = await table.Run.ClaimClosingAsync(Cancel);
        await File.WriteAllTextAsync(
            Path.Combine(_runs, table.Id, PlaytestRun.ClosingFile),
            JsonSerializer.Serialize(new { By = "stopped", At = DateTimeOffset.UtcNow - PlaytestRun.ClosingHeld - TimeSpan.FromMinutes(1) }, ArtifactJson.DocumentOptions),
            Cancel);
        var takenOver = await there.Run.ClaimClosingAsync(Cancel);

        first.ShouldBeTrue();
        second.ShouldBeFalse("the first run's claim stands");
        again.ShouldBeTrue("a run's own claim is its own");
        takenOver.ShouldBeTrue("a claim that ran out is a process that stopped");
    }

    [Fact]
    public async Task A_run_whose_claim_another_took_over_finds_out_before_it_writes()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, HostedTables.OnePerson);
        using var elsewhere = _there.Registry();
        var there = (await elsewhere.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        (await table.Run!.ClaimClosingAsync(Cancel)).ShouldBeTrue();
        var renewed = await table.Run.StillClosingAsync(Cancel);
        await File.WriteAllTextAsync(
            Path.Combine(_runs, table.Id, PlaytestRun.ClosingFile),
            JsonSerializer.Serialize(new { By = "stalled", At = DateTimeOffset.UtcNow - PlaytestRun.ClosingHeld - TimeSpan.FromMinutes(1) }, ArtifactJson.DocumentOptions),
            Cancel);
        (await there.Run!.ClaimClosingAsync(Cancel)).ShouldBeTrue();

        var stillHere = await table.Run.StillClosingAsync(Cancel);

        renewed.ShouldBeTrue();
        stillHere.ShouldBeFalse("the claim is another run's now");
        (await there.Run.StillClosingAsync(Cancel)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_checkpoint_never_writes_over_the_trace_another_process_finished()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, Randoms);
        using var elsewhere = _there.Registry();
        var there = (await elsewhere.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull();
        (await ((IAgentTurnTaker)there.Session).TakeTurnAsync(Cancel)).ShouldBe(AgentTurn.Taken);
        (await table.Session.CatchUpAsync(Cancel)).ShouldBeTrue();
        var path = Path.Combine(_runs, table.Id, RunRecorder.TracesDirectory, $"{table.Session.MatchId}.json");
        await table.Run!.CheckpointAsync(table.Session.MatchId, Cancel);
        var checkpointed = await File.ReadAllTextAsync(path, Cancel);
        const string finished = "{ \"outcome\": { \"reason\": \"Elimination\" } }";
        await File.WriteAllTextAsync(path, finished, Cancel);

        await table.Run.CheckpointAsync(table.Session.MatchId, Cancel);

        checkpointed.ShouldContain("entries", Case.Insensitive);
        (await File.ReadAllTextAsync(path, Cancel)).ShouldBe(finished, "the match's trace with its outcome is the last word on it");
    }

    [Fact]
    public async Task No_run_claims_the_closing_of_a_session_whose_record_says_it_finished()
    {
        using var registry = _here.Registry();
        var table = await RecordedAsync(registry, HostedTables.OnePerson);
        await table.Run!.MarkAsync(TableRecord.Finished, Cancel);

        var claimed = await table.Run.ClaimClosingAsync(Cancel);

        claimed.ShouldBeFalse();
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

    /// <summary>A table's bots' turns, taken here until the match is over.</summary>
    private static async Task PlayedOutAsync(IAgentTurnTaker table)
    {
        for (var turn = 0; turn < 2000 && await table.TakeTurnAsync(Cancel) != AgentTurn.Done; turn++)
        {
        }
    }

    /// <summary>Waits for a table's match to end and its session to be written, here or elsewhere.</summary>
    private static async Task ClosedAsync(PlayedTable table)
    {
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), Cancel);
        for (var waited = 0; waited < 300 && !(table.IsFinished && table.Run!.IsClosed); waited++)
        {
            await Task.Delay(50, Cancel);
        }

        table.IsFinished.ShouldBeTrue();
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
