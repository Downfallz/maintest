using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Infrastructure.Matches;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Infrastructure.Tests.Matches;

/// <summary>
/// The turns a table's bots owe, taken in the process that holds it (ADR 0159, phase 5): one at a time until
/// nothing more is owed, again after a wait when the store is out, and never for a table let go of.
/// </summary>
public sealed class InProcessAgentTurnsTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Turns_are_taken_until_nothing_more_is_owed()
    {
        var table = new Scripted(AgentTurn.Taken, AgentTurn.Taken, AgentTurn.Done);
        var turns = new InProcessAgentTurns(TimeProvider.System);

        turns.Owe(table);
        await table.Finished.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        table.Taken.ShouldBe(3);
    }

    [Fact]
    public async Task A_table_owed_again_while_its_turns_are_taken_is_never_taken_twice_at_once()
    {
        var table = new Scripted(AgentTurn.Taken, AgentTurn.Taken, AgentTurn.Taken, AgentTurn.Done) { Slow = true };
        var turns = new InProcessAgentTurns(TimeProvider.System);

        for (var owed = 0; owed < 20; owed++)
        {
            turns.Owe(table);
        }

        await table.Finished.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await Task.Delay(100, Cancel);

        table.MostAtOnce.ShouldBe(1);
    }

    [Fact]
    public async Task A_turn_the_store_failed_is_taken_again_after_a_wait()
    {
        var clock = new FakeTimeProvider();
        var table = new Scripted(AgentTurn.Failed, AgentTurn.Done);
        var turns = new InProcessAgentTurns(clock);

        turns.Owe(table);
        await table.TakenAtLeast(1).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await Task.Delay(50, Cancel);
        var beforeTheWait = table.Taken;
        clock.Advance(InProcessAgentTurns.FirstRetry);
        await table.Finished.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        beforeTheWait.ShouldBe(1);
        table.Taken.ShouldBe(2);
    }

    [Fact]
    public async Task Nothing_is_taken_for_a_table_let_go_of()
    {
        using var stopping = new CancellationTokenSource();
        var table = new Scripted(AgentTurn.Taken, AgentTurn.Done) { Stopping = stopping.Token };
        await stopping.CancelAsync();
        var turns = new InProcessAgentTurns(TimeProvider.System);

        turns.Owe(table);
        await Task.Delay(100, Cancel);

        table.Taken.ShouldBe(0);
    }

    /// <summary>A table whose turns go as scripted, the last one repeated, counting how many are taken at once.</summary>
    private sealed class Scripted(params AgentTurn[] script) : IAgentTurnTaker
    {
        private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<(int Count, TaskCompletionSource Reached)> _waiting = [];
        private int _taken;
        private int _now;
        private int _most;

        public string Run => "table-1";

        public CancellationToken Stopping { get; init; }

        public string? LastFailure => null;

        public bool Slow { get; init; }

        public int Taken => Volatile.Read(ref _taken);

        public int MostAtOnce => Volatile.Read(ref _most);

        public Task Finished => _finished.Task;

        public Task TakenAtLeast(int count)
        {
            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_waiting)
            {
                if (Taken >= count)
                {
                    reached.SetResult();
                }
                else
                {
                    _waiting.Add((count, reached));
                }
            }

            return reached.Task;
        }

        public async Task<AgentTurn> TakeTurnAsync(CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _now);
            InterlockedMax(ref _most, now);
            if (Slow)
            {
                await Task.Delay(20, cancellationToken);
            }

            var taken = Interlocked.Increment(ref _taken);
            Interlocked.Decrement(ref _now);
            lock (_waiting)
            {
                foreach (var waiting in _waiting.Where(waiting => taken >= waiting.Count).ToList())
                {
                    waiting.Reached.SetResult();
                    _waiting.Remove(waiting);
                }
            }

            var turn = script[Math.Min(taken - 1, script.Length - 1)];
            if (turn == AgentTurn.Done)
            {
                _finished.TrySetResult();
            }

            return turn;
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int seen;
            while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen)
            {
            }
        }
    }
}
