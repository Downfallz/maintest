namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// A table whose Player agents can take a turn (ADR 0159): one of their turns at a time, each written to the
/// journal on the condition that nobody wrote first, the same door a person's decision comes through.
/// </summary>
public interface IAgentTurnTaker
{
    /// <summary>The table's run, by which a process that does not hold it picks it up.</summary>
    string Run { get; }

    /// <summary>Cancelled once the table is let go of here: nothing is taken for it after that.</summary>
    CancellationToken Stopping { get; }

    /// <summary>Why the last turn answered <see cref="AgentTurn.Failed" />, as a dead letter says it; none before any did.</summary>
    string? LastFailure { get; }

    /// <summary>
    /// Takes one turn of the agents from where the journal ends, and says how it went. Never throws for the
    /// table's own sake: a table that cannot be played on is ended, and says why, and answers <see cref="AgentTurn.Done" />.
    /// </summary>
    Task<AgentTurn> TakeTurnAsync(CancellationToken cancellationToken = default);
}
