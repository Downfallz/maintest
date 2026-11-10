namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// One line of a decision journal: a decision a seat took (<see cref="DecisionEntry" />), a seat conceding
/// (<see cref="ConcessionEntry" />), or a seat changing hands (<see cref="SeatChangeEntry" />). In order, with how
/// the match was created, they are everything that happened to the match (ADR 0159).
/// </summary>
public interface IJournalEntry;
