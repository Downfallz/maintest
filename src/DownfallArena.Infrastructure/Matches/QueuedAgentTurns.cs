using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Ports;

namespace DownfallArena.Infrastructure.Matches;

/// <summary>
/// The turns a table's bots owe, handed to a worker through a queue (ADR 0159, phase 5): told a table owes one, it
/// puts the table on the queue, and the worker that receives it takes the turns (<see cref="AgentTurnWorker" />).
/// </summary>
/// <remarks>
/// A queue that does not answer is tried again, a second later and then twice as long each time up to half a
/// minute, for as long as the table is held here: a turn never put on the queue is a turn nobody takes.
/// </remarks>
public sealed class QueuedAgentTurns(IWorkChannel turns, TimeProvider clock) : IAgentTurns
{
    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LongestRetry = TimeSpan.FromSeconds(30);

    /// <summary>What is said of a turn that could not be put on the queue: the host's console.</summary>
    public Action<string> Said { get; init; } = _ => { };

    public void Owe(IAgentTurnTaker table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _ = Task.Run(() => QueuedAsync(table), CancellationToken.None);
    }

    private async Task QueuedAsync(IAgentTurnTaker table)
    {
        var body = new OwedTurn(table.Run).ToJson();
        var wait = FirstRetry;
        while (!table.Stopping.IsCancellationRequested)
        {
            try
            {
                await turns.SendAsync(body, cancellationToken: table.Stopping);
                return;
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                Said($"  Session {table.Run}: its bots' turn could not be put on {turns.Name} ({failure.Message}); tried again in {wait.TotalSeconds:0.##} s.");
            }

            try
            {
                await Task.Delay(wait, clock, table.Stopping);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            wait = wait + wait < LongestRetry ? wait + wait : LongestRetry;
        }
    }
}
