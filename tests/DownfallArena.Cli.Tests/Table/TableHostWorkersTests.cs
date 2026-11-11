using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The workers of the queued turns as the table host starts them (ADR 0159, phase 5): none when the turns are taken
/// in the process, both until the host stops otherwise, and the turns taken in the process when a queue cannot be
/// opened.
/// </summary>
public sealed class TableHostWorkersTests : IDisposable
{
    private readonly HostedTables _hosted = new();

    public void Dispose() => _hosted.Dispose();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task No_worker_runs_when_the_turns_are_taken_in_the_process()
    {
        using var registry = _hosted.Registry();

        var working = await TableHost.StartWorkersAsync(null, null, registry, TimeProvider.System, Cancel);

        working.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Both_workers_run_until_the_host_stops()
    {
        using var registry = _hosted.Registry();
        var channels = new InMemoryWorkChannels(TimeProvider.System);
        var turns = await channels.OpenAsync(AgentTurnWorker.Queue, Cancel);
        using var stopping = new CancellationTokenSource();

        var working = await TableHost.StartWorkersAsync(channels, turns, registry, TimeProvider.System, stopping.Token);
        var runningBeforeTheStop = !working.IsCompleted;
        await stopping.CancelAsync();
        await working.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        runningBeforeTheStop.ShouldBeTrue();
    }

    [Fact]
    public async Task A_queue_that_cannot_be_opened_leaves_the_turns_in_the_process()
    {
        var turns = await TableHost.OpenChannelAsync(new Unopenable(), AgentTurnWorker.Queue, Cancel);

        turns.ShouldBeNull();
    }

    private sealed class Unopenable : IWorkChannels
    {
        public Task<IWorkChannel> OpenAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromException<IWorkChannel>(new IOException("The account refuses this identity its queues."));
    }
}
