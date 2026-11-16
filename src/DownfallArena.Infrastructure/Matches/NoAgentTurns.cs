using DownfallArena.Application.Matches.Journaling;

namespace DownfallArena.Infrastructure.Matches;

/// <summary>
/// Turns nobody takes here (ADR 0162): a process that picks tables up for something else -- the hourly job expiring
/// an invitation -- holds their bots back, and leaves their turns to the process whose work they are.
/// </summary>
public sealed class NoAgentTurns : IAgentTurns
{
    public void Owe(IAgentTurnTaker table)
    {
        // Not this process's to take.
    }
}
