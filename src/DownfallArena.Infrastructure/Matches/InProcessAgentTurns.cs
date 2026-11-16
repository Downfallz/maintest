using System.Runtime.CompilerServices;
using DownfallArena.Application.Matches.Journaling;

namespace DownfallArena.Infrastructure.Matches;

/// <summary>
/// The turns a table's Player agents owe, taken in the background of the process that holds the table: what a
/// laptop and a debugger run (ADR 0159). One table's turns are taken one after the other, never two at once.
/// </summary>
/// <remarks>
/// It does not give up on a store that is out: a turn that could not be written is taken again after a wait that
/// grows to half a minute, for as long as the table is held. Nobody may be reading the table to set it off again
/// -- a page closed on the bot's turn is the very case a notice exists for (ADR 0118). Another writer taking the
/// match further a few times in a row is waited out the same way.
/// </remarks>
public sealed class InProcessAgentTurns(TimeProvider clock) : IAgentTurns
{
    /// <summary>The first wait before a turn is taken again.</summary>
    public static readonly TimeSpan FirstRetry = TimeSpan.FromMilliseconds(250);

    /// <summary>The longest wait before a turn is taken again.</summary>
    public static readonly TimeSpan LongestRetry = TimeSpan.FromSeconds(30);

    /// <summary>How often in a row another writer may take the match further before a turn waits a moment.</summary>
    public const int MovedInARow = 3;

    private readonly ConditionalWeakTable<IAgentTurnTaker, Taking> _taking = [];

    /// <summary>
    /// Sets the table's turns off, unless they are being taken: then whoever is taking them looks once more when
    /// done, since what was owed may have come after its last look.
    /// </summary>
    public void Owe(IAgentTurnTaker table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var taking = _taking.GetValue(table, _ => new Taking());
        if (Interlocked.Exchange(ref taking.State, 2) == 0)
        {
            _ = Task.Run(() => TurnsAsync(table, taking), CancellationToken.None);
        }
    }

    private async Task TurnsAsync(IAgentTurnTaker table, Taking taking)
    {
        do
        {
            Volatile.Write(ref taking.State, 1);
            await TakenAsync(table);
        }
        while (Interlocked.CompareExchange(ref taking.State, 0, 1) != 1);
    }

    /// <summary>The turns owed, one at a time, until nothing more is owed or the table is let go of.</summary>
    private async Task TakenAsync(IAgentTurnTaker table)
    {
        var wait = FirstRetry;
        var moved = 0;
        while (!table.Stopping.IsCancellationRequested)
        {
            var turn = await table.TakeTurnAsync(table.Stopping);
            if (turn == AgentTurn.Done)
            {
                return;
            }

            if (turn == AgentTurn.Taken)
            {
                moved = 0;
                wait = FirstRetry;
                continue;
            }

            if (turn == AgentTurn.Moved && ++moved < MovedInARow)
            {
                continue;
            }

            // The store is out, or another writer keeps taking the match further: not at once again.
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

    /// <summary>Whether a table's turns are being taken (1), and asked for again meanwhile (2), or neither (0).</summary>
    private sealed class Taking
    {
        public int State;
    }
}
