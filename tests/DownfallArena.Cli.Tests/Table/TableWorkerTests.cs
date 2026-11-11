using DownfallArena.Application.Learning.Recording;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The worker app (ADR 0162, phase 6): a host puts its tables' bots' turns on a queue, and a process of its own takes
/// them, plays the match to its end and writes it, while the host, which takes none, reads the match where the worker
/// left it and serves what the worker wrote.
/// </summary>
public sealed class TableWorkerTests : IDisposable
{
    private readonly InMemoryWorkChannels _channels = new(TimeProvider.System);
    private readonly HostedTables _hosted;

    public TableWorkerTests()
    {
        var queue = _channels.OpenAsync(AgentTurnWorker.Queue).GetAwaiter().GetResult();
        _hosted = new HostedTables(directory => new FileArtifactStore(directory), turns: new QueuedAgentTurns(queue, TimeProvider.System));
    }

    public void Dispose() => _hosted.Dispose();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_host_s_bots_turns_are_taken_by_the_worker_which_writes_the_match_the_host_then_serves()
    {
        using var host = _hosted.Registry();
        var table = await _hosted.Composer.ComposeAsync(HostedTables.Bots, Cancel);
        using (var place = (await host.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await host.AddAsync(table, place, Cancel);
        }

        await table.RecordAsync(Cancel);
        using var services = Services(out var options);
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var over = new TableWorker.Over(new FileArtifactStore(_hosted.RunsDirectory), _hosted.Index, _channels);
        var working = TableWorker.WorkAsync(services.Services, HostedTables.Rules, over, sender: null, stopping.Token);

        for (var waited = 0; waited < 600 && (await _hosted.Stored!.RecordAsync(table.Id, Cancel))?.Status != TableRecord.Finished; waited++)
        {
            await Task.Delay(100, Cancel);
        }

        await table.Session.CatchUpAsync(Cancel);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), Cancel);
        for (var waited = 0; waited < 300 && !(table.IsFinished && table.Run!.IsClosed); waited++)
        {
            await Task.Delay(50, Cancel);
        }

        await stopping.CancelAsync();
        await working;

        options.Command.ShouldBe(CliOptions.WorkerCommand);
        (await _hosted.Stored!.RecordAsync(table.Id, Cancel)).ShouldNotBeNull().Status.ShouldBe(TableRecord.Finished);
        table.Run!.IsClosed.ShouldBeTrue("the host serves the session the worker wrote");
        var episodes = await File.ReadAllLinesAsync(Path.Combine(_hosted.RunsDirectory, table.Id, RunRecorder.EpisodesFile), Cancel);
        episodes.Length.ShouldBe(2, "written once, by the worker");
    }

    [Fact]
    public async Task The_command_refuses_a_directory_whose_queues_no_host_shares()
    {
        using var services = Services(out var options);

        var exit = await TableWorker.RunAsync(services.Services, options, HostedTables.Rules);

        exit.ShouldBe(1);
    }

    /// <summary>Services built for this very command, as the command line builds them: what it needs is registered for it.</summary>
    private Microsoft.Extensions.Hosting.IHost Services(out CliOptions options)
    {
        options = CliOptions.Parse([CliOptions.WorkerCommand, "--record", _hosted.RunsDirectory]) with { SchemaPath = _hosted.SchemaPath };
        return CliHost.Build(options, seed: 7, logMatchToConsole: false);
    }
}
