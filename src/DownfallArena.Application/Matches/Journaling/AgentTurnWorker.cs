using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// The worker that takes the turns a queue says tables owe (ADR 0159, phase 5): for each item, the table is found
/// -- held, or picked up from its record and journal -- and its bots' turns are taken until nothing more is owed.
/// </summary>
/// <remarks>
/// <para>
/// It retries the way a queue-triggered function does: an item whose turn failed comes back, later each time, and
/// once it has been delivered <see cref="MostDeliveries" /> times it goes on the poison queue
/// (<c>&lt;queue&gt;-poison</c>) with why its last delivery failed, for the second worker to keep
/// (<see cref="DeadLetterWorker" />). A table nobody can take turns at any more -- over, closed, gone -- owes
/// nothing, and its item is done with.
/// </para>
/// <para>
/// Another writer moving the match is not a failure: the item is put back a moment later as a new one, so a busy
/// table is never poisoned for being busy. Nor is a long run of turns: past half the time an item is hidden for,
/// the rest is handed on as a new item, so no turn is taken under an item that has come back to someone else.
/// </para>
/// </remarks>
public sealed class AgentTurnWorker(
    IWorkChannel turns,
    IWorkChannel poison,
    Func<string, CancellationToken, Task<IAgentTurnTaker?>> find,
    TimeProvider clock)
{
    /// <summary>The queue the turns tables owe are put on.</summary>
    public const string Queue = "agent-turns";

    /// <summary>How many times an item is delivered before it is given up on, as a queue trigger's own default.</summary>
    public const int MostDeliveries = 5;

    /// <summary>How long an item is hidden from every other worker while its turns are taken.</summary>
    public static readonly TimeSpan HiddenFor = TimeSpan.FromMinutes(1);

    /// <summary>The first wait before a failed item comes back; each one after waits twice as long, up to a minute.</summary>
    public static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(2);

    /// <summary>How long before an item put back for a busy table comes back.</summary>
    public static readonly TimeSpan BusyFor = TimeSpan.FromSeconds(1);

    /// <summary>How often in a row another writer may move the match before the item is put back.</summary>
    public const int MovedInARow = 3;

    private static readonly TimeSpan LongestRetry = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FirstIdle = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan LongestIdle = TimeSpan.FromSeconds(5);

    /// <summary>What the worker says of an item it retries or gives up on: the host's console.</summary>
    public Action<string> Said { get; init; } = _ => { };

    /// <summary>One look at the queue: every item it hands out taken. How many items there were.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var received = await turns.ReceiveAsync(16, HiddenFor, cancellationToken);
        foreach (var work in received)
        {
            await TakeAsync(work, cancellationToken);
        }

        return received.Count;
    }

    /// <summary>
    /// Looks at the queue until stopped: again at once after a look that found items, and after a wait that grows
    /// from a quarter of a second to five while it finds none. A look that fails is said, and waited out.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var idle = FirstIdle;
        while (!cancellationToken.IsCancellationRequested)
        {
            var found = 0;
            try
            {
                found = await RunOnceAsync(cancellationToken);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                Said($"  The queue {turns.Name} could not be read: {failure.Message}");
            }

            if (found > 0)
            {
                idle = FirstIdle;
                continue;
            }

            try
            {
                await Task.Delay(idle, clock, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            idle = idle + idle < LongestIdle ? idle + idle : LongestIdle;
        }
    }

    private async Task TakeAsync(QueuedWork work, CancellationToken cancellationToken)
    {
        if (OwedTurn.Parse(work.Body) is not { } owed)
        {
            await PoisonAsync(work, "Not a turn a table owes: the item cannot be read as one.", cancellationToken);
            return;
        }

        try
        {
            if (await find(owed.Run, cancellationToken) is not { } table)
            {
                // Over, closed, gone, or never a table: nothing is owed any more.
                await turns.DeleteAsync(work, cancellationToken);
                return;
            }

            await TurnsAsync(work, owed, table, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopping: the item comes back once its time is up, for whoever looks next.
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            await FailedAsync(work, $"{failure.GetType().Name}: {failure.Message}", cancellationToken);
        }
    }

    /// <summary>The table's turns, taken until nothing more is owed, the item handed on, or a turn failed.</summary>
    private async Task TurnsAsync(QueuedWork work, OwedTurn owed, IAgentTurnTaker table, CancellationToken cancellationToken)
    {
        var started = clock.GetUtcNow();
        var moved = 0;
        while (true)
        {
            switch (await table.TakeTurnAsync(cancellationToken))
            {
                case AgentTurn.Done:
                    await turns.DeleteAsync(work, cancellationToken);
                    return;
                case AgentTurn.Taken when clock.GetUtcNow() - started > HiddenFor / 2:
                    await HandOnAsync(work, owed, TimeSpan.Zero, cancellationToken);
                    return;
                case AgentTurn.Taken:
                    moved = 0;
                    break;
                case AgentTurn.Moved when ++moved < MovedInARow:
                    break;
                case AgentTurn.Moved:
                    await HandOnAsync(work, owed, BusyFor, cancellationToken);
                    return;
                default:
                    await FailedAsync(work, table.LastFailure ?? "The bots' turn could not be taken: the store did not answer.", cancellationToken);
                    return;
            }
        }
    }

    /// <summary>The rest of the item's turns, handed on as a new item: its deliveries start again, since nothing failed.</summary>
    private async Task HandOnAsync(QueuedWork work, OwedTurn owed, TimeSpan after, CancellationToken cancellationToken)
    {
        await turns.SendAsync(owed.ToJson(), hiddenFor: after, cancellationToken: cancellationToken);
        await turns.DeleteAsync(work, cancellationToken);
    }

    /// <summary>A delivery that failed: the item comes back later, or, at its last delivery, goes on the poison queue.</summary>
    private async Task FailedAsync(QueuedWork work, string error, CancellationToken cancellationToken)
    {
        if (work.Delivered >= MostDeliveries)
        {
            await PoisonAsync(work, error, cancellationToken);
            return;
        }

        var wait = Wait(work.Delivered);
        await turns.RetryAsync(work, wait, cancellationToken);
        Said($"  {work.Body} failed at delivery {work.Delivered} of {MostDeliveries} ({error}); it comes back in {wait.TotalSeconds:0.##} s.");
    }

    private async Task PoisonAsync(QueuedWork work, string error, CancellationToken cancellationToken)
    {
        var poisoned = new PoisonedWork(turns.Name, work.Body, error, work.Delivered, work.QueuedAt, clock.GetUtcNow());
        await poison.SendAsync(poisoned.ToJson(), cancellationToken: cancellationToken);
        await turns.DeleteAsync(work, cancellationToken);
        Said($"  {work.Body} given up on after {work.Delivered} deliveries, onto {poison.Name}: {error}");
    }

    /// <summary>How long a failed item waits before it comes back: twice as long at each delivery, up to a minute.</summary>
    private static TimeSpan Wait(long delivered)
    {
        var wait = FirstRetry;
        for (var at = 1; at < delivered && wait < LongestRetry; at++)
        {
            wait += wait;
        }

        return wait < LongestRetry ? wait : LongestRetry;
    }
}
