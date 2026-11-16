using System.Text.Json.Nodes;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Work;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A table's bots answer through a port (ADR 0159, phase 5): the table says it owes their turns, whoever takes
/// them takes one at a time through the journal's door, and the dice of a turn are that turn's, so a process that
/// picked the table up takes the turn the process that held it would have taken.
/// </summary>
public sealed class AgentTurnsTests : IDisposable
{
    private static readonly TableRequest Randoms = new(AgentSpec.Parse("random"), AgentSpec.Parse("random"), Who: null, Handover: null, Seed: 11);

    private readonly Owed _owed = new();
    private readonly Owed _elsewhere = new();
    private readonly HostedTables _hosted;
    private readonly HostedTables _other;

    public AgentTurnsTests()
    {
        _hosted = new HostedTables(directory => new FileArtifactStore(directory), turns: _owed);
        _other = new HostedTables(directory => new FileArtifactStore(directory), turns: _elsewhere);
    }

    public void Dispose()
    {
        _hosted.Dispose();
        _other.Dispose();
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_table_owes_its_bots_turns_to_whoever_takes_them_and_takes_none_itself()
    {
        using var registry = _hosted.Registry();
        var table = await RecordedAsync(registry);
        var taker = (IAgentTurnTaker)table.Session;

        _owed.Tables.ShouldContain(taker);
        (await LinesAsync(_hosted, table.Id)).ShouldBeEmpty();

        var turn = await taker.TakeTurnAsync(Cancel);

        turn.ShouldBe(AgentTurn.Taken);
        (await LinesAsync(_hosted, table.Id)).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task A_host_that_picked_a_table_up_takes_the_bots_next_turn_as_the_host_that_held_it_would()
    {
        using var registry = _hosted.Registry();
        var table = await RecordedAsync(registry);
        var held = (IAgentTurnTaker)table.Session;
        for (var turn = 0; turn < 4; turn++)
        {
            (await held.TakeTurnAsync(Cancel)).ShouldBe(AgentTurn.Taken);
        }

        // The run as it stands, given to another host: the same record and journal, and nothing of this process.
        CopyRun(table.Id, _hosted.RunsDirectory, _other.RunsDirectory);
        using var elsewhere = _other.Registry();
        var picked = (IAgentTurnTaker)(await elsewhere.ByIdAsync(table.Id, Cancel)).ShouldNotBeNull().Session;
        var before = (await LinesAsync(_hosted, table.Id)).Count;

        await held.TakeTurnAsync(Cancel);
        await picked.TakeTurnAsync(Cancel);

        var here = await LinesAsync(_hosted, table.Id);
        var there = await LinesAsync(_other, table.Id);
        here.Count.ShouldBeGreaterThan(before);
        there.Skip(before).ShouldBe(here.Skip(before));
    }

    [Fact]
    public async Task A_table_whose_bots_owe_their_turns_through_a_queue_is_played_to_its_end_by_the_worker()
    {
        var channels = new InMemoryWorkChannels(TimeProvider.System);
        var turns = await channels.OpenAsync(AgentTurnWorker.Queue, Cancel);
        using var queued = new HostedTables(directory => new FileArtifactStore(directory), turns: new QueuedAgentTurns(turns, TimeProvider.System));
        using var registry = queued.Registry();
        var table = await queued.Composer.ComposeAsync(HostedTables.Bots, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        await table.RecordAsync(Cancel);
        var worker = new AgentTurnWorker(
            turns,
            await channels.OpenAsync(PoisonedWork.QueueOf(AgentTurnWorker.Queue), Cancel),
            async (run, cancellationToken) => (await registry.ByIdAsync(run, cancellationToken))?.Session as IAgentTurnTaker,
            TimeProvider.System);

        for (var look = 0; look < 600 && !table.IsOver; look++)
        {
            if (await worker.RunOnceAsync(Cancel) == 0)
            {
                await Task.Delay(10, Cancel);
            }
        }

        table.IsOver.ShouldBeTrue("the worker took every turn the queue said the table owed");
        (await table.Session.Outcome).IsSuccess.ShouldBeTrue();
    }

    /// <summary>A table of two random bots, added and written down, nothing played yet.</summary>
    private async Task<PlayedTable> RecordedAsync(TableRegistry registry)
    {
        var table = await _hosted.Composer.ComposeAsync(Randoms, Cancel);
        using var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull();
        await registry.AddAsync(table, place, Cancel);
        await table.RecordAsync(Cancel);
        return table;
    }

    /// <summary>The decisions a table's journal holds, each as written but for when.</summary>
    private static async Task<List<string>> LinesAsync(HostedTables hosted, string id)
    {
        var path = Path.Combine(hosted.RunsDirectory, id, PlaytestRun.JournalFile);
        var text = File.Exists(path) ? await File.ReadAllTextAsync(path, Cancel) : string.Empty;
        return [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            var parsed = JsonNode.Parse(line)!.AsObject();
            parsed.Remove("at");
            return parsed.ToJsonString();
        })];
    }

    private static void CopyRun(string id, string from, string to)
    {
        var target = Directory.CreateDirectory(Path.Combine(to, id));
        foreach (var file in Directory.EnumerateFiles(Path.Combine(from, id)))
        {
            File.Copy(file, Path.Combine(target.FullName, Path.GetFileName(file)));
        }
    }

    /// <summary>Takes no turn: remembers which tables said they owe one, for the test to take them.</summary>
    private sealed class Owed : IAgentTurns
    {
        private readonly List<IAgentTurnTaker> _tables = [];

        public IReadOnlyList<IAgentTurnTaker> Tables
        {
            get
            {
                lock (_tables)
                {
                    return [.. _tables];
                }
            }
        }

        public void Owe(IAgentTurnTaker table)
        {
            lock (_tables)
            {
                _tables.Add(table);
            }
        }
    }
}
