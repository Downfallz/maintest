namespace DownfallArena.Application.Matches.Journaling;

/// <summary>How one turn of a table's Player agents went (<see cref="IAgentTurnTaker.TakeTurnAsync" />).</summary>
public enum AgentTurn
{
    /// <summary>Nothing more is owed: a person is asked, the match is over, or the table cannot be played on.</summary>
    Done,

    /// <summary>A turn was taken, and the agents owe another.</summary>
    Taken,

    /// <summary>Another writer took the match further first: it is read again before the next turn.</summary>
    Moved,

    /// <summary>The store could not be reached: the turn is to be taken again after a wait.</summary>
    Failed,
}
