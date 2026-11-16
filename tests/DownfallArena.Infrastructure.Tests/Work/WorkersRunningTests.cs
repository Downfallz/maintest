using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Work;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Infrastructure.Tests.Work;

/// <summary>
/// The workers as a host runs them (ADR 0159, phase 5): looking at their queue until stopped, and the queued turns
/// putting a table on its queue, again while the queue does not answer.
/// </summary>
public sealed class WorkersRunningTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_turn_worker_takes_what_is_queued_while_it_runs_and_stops_when_told()
    {
        var channels = new InMemoryWorkChannels(TimeProvider.System);
        var turns = await channels.OpenAsync(AgentTurnWorker.Queue, Cancel);
        var table = new Scripted("table-1");
        var worker = new AgentTurnWorker(turns, await channels.OpenAsync(PoisonedWork.QueueOf(turns.Name), Cancel), (_, _) => Task.FromResult<IAgentTurnTaker?>(table), TimeProvider.System);
        using var stopping = new CancellationTokenSource();

        var running = worker.RunAsync(stopping.Token);
        await turns.SendAsync(new OwedTurn("table-1").ToJson(), cancellationToken: Cancel);
        await table.Taken.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await stopping.CancelAsync();

        await running.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        ((InMemoryWorkChannel)turns).Bodies.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_second_worker_keeps_what_is_poisoned_while_it_runs_and_stops_when_told()
    {
        var channels = new InMemoryWorkChannels(TimeProvider.System);
        var poison = await channels.OpenAsync("agent-turns-poison", Cancel);
        var letters = (InMemoryWorkChannel)await channels.OpenAsync("agent-turns-dlq", Cancel);
        await poison.SendAsync("raw", cancellationToken: Cancel);
        var keeper = new DeadLetterWorker(poison, letters, TimeProvider.System, "replica-7");
        using var stopping = new CancellationTokenSource();

        var running = keeper.RunAsync(stopping.Token);
        for (var waited = 0; letters.Bodies.Count == 0 && waited < 200; waited++)
        {
            await Task.Delay(25, Cancel);
        }

        await stopping.CancelAsync();
        await running.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        letters.Bodies.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_worker_whose_queue_does_not_answer_says_so_and_goes_on_looking()
    {
        var said = new List<string>();
        var worker = new AgentTurnWorker(new Unanswering(), new Unanswering(), (_, _) => Task.FromResult<IAgentTurnTaker?>(null), TimeProvider.System) { Said = line => { lock (said) { said.Add(line); } } };
        var keeper = new DeadLetterWorker(new Unanswering(), new Unanswering(), TimeProvider.System, "replica-7") { Said = line => { lock (said) { said.Add(line); } } };
        using var stopping = new CancellationTokenSource();

        var running = Task.WhenAll(worker.RunAsync(stopping.Token), keeper.RunAsync(stopping.Token));
        await Task.Delay(200, Cancel);
        await stopping.CancelAsync();
        await running.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        lock (said)
        {
            said.ShouldContain(line => line.Contains("could not be read", StringComparison.Ordinal) && line.Contains("unanswering", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task A_turn_the_queue_did_not_take_is_put_on_it_again_after_a_wait()
    {
        var clock = new FakeTimeProvider();
        var channel = new Flaky(await new InMemoryWorkChannels(clock).OpenAsync("agent-turns", Cancel)) { Refusals = 1 };
        var said = new List<string>();
        var turns = new QueuedAgentTurns(channel, clock) { Said = said.Add };

        turns.Owe(new Scripted("table-1"));
        for (var waited = 0; said.Count == 0 && waited < 200; waited++)
        {
            await Task.Delay(10, Cancel);
        }

        // The wait may start after the line is said: the clock is moved on until the wait has seen it move.
        for (var waited = 0; channel.Sent == 0 && waited < 200; waited++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(10, Cancel);
        }

        said.ShouldHaveSingleItem().ShouldContain("could not be put on agent-turns");
        channel.Sent.ShouldBe(1);
    }

    [Fact]
    public async Task Nothing_is_put_on_the_queue_for_a_table_let_go_of()
    {
        var channel = new Flaky(await new InMemoryWorkChannels(TimeProvider.System).OpenAsync("agent-turns", Cancel));
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();
        var turns = new QueuedAgentTurns(channel, TimeProvider.System);

        turns.Owe(new Scripted("table-1") { Stopping = stopping.Token });
        await Task.Delay(100, Cancel);

        channel.Sent.ShouldBe(0);
    }

    private sealed class Scripted(string run) : IAgentTurnTaker
    {
        private readonly TaskCompletionSource _taken = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Run => run;

        public CancellationToken Stopping { get; init; }

        public string? LastFailure => null;

        public Task Taken => _taken.Task;

        public Task<AgentTurn> TakeTurnAsync(CancellationToken cancellationToken = default)
        {
            _taken.TrySetResult();
            return Task.FromResult(AgentTurn.Done);
        }
    }

    /// <summary>A queue that is out.</summary>
    private sealed class Unanswering : IWorkChannel
    {
        public string Name => "unanswering";

        public Task SendAsync(string body, TimeSpan? keptFor = null, TimeSpan? hiddenFor = null, CancellationToken cancellationToken = default) => Out();

        public Task<IReadOnlyList<QueuedWork>> ReceiveAsync(int most, TimeSpan hiddenFor, CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<QueuedWork>>(new IOException("The queue is out."));

        public Task DeleteAsync(QueuedWork work, CancellationToken cancellationToken = default) => Out();

        public Task RetryAsync(QueuedWork work, TimeSpan after, CancellationToken cancellationToken = default) => Out();

        public Task<IReadOnlyList<PeekedWork>> PeekAsync(int most, CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<PeekedWork>>(new IOException("The queue is out."));

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromException<int>(new IOException("The queue is out."));

        private static Task Out() => Task.FromException(new IOException("The queue is out."));
    }

    /// <summary>A queue that refuses its first sends, counting those it takes.</summary>
    private sealed class Flaky(IWorkChannel inner) : IWorkChannel
    {
        private int _sent;

        public int Refusals { get; set; }

        public int Sent => Volatile.Read(ref _sent);

        public string Name => inner.Name;

        public async Task SendAsync(string body, TimeSpan? keptFor = null, TimeSpan? hiddenFor = null, CancellationToken cancellationToken = default)
        {
            if (Refusals > 0)
            {
                Refusals--;
                throw new IOException("The queue is out.");
            }

            await inner.SendAsync(body, keptFor, hiddenFor, cancellationToken);
            Interlocked.Increment(ref _sent);
        }

        public Task<IReadOnlyList<QueuedWork>> ReceiveAsync(int most, TimeSpan hiddenFor, CancellationToken cancellationToken = default) => inner.ReceiveAsync(most, hiddenFor, cancellationToken);

        public Task DeleteAsync(QueuedWork work, CancellationToken cancellationToken = default) => inner.DeleteAsync(work, cancellationToken);

        public Task RetryAsync(QueuedWork work, TimeSpan after, CancellationToken cancellationToken = default) => inner.RetryAsync(work, after, cancellationToken);

        public Task<IReadOnlyList<PeekedWork>> PeekAsync(int most, CancellationToken cancellationToken = default) => inner.PeekAsync(most, cancellationToken);

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => inner.CountAsync(cancellationToken);
    }
}
