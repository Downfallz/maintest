namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// Where the turns a table's Player agents owe are taken (ADR 0159, phase 5): told a table owes one, it has
/// them taken, one at a time, until nothing more is owed. In the process that holds the table by default; a
/// worker's, through a queue, otherwise. Telling it again while the turns are being taken costs one more look.
/// </summary>
public interface IAgentTurns
{
    void Owe(IAgentTurnTaker table);
}
