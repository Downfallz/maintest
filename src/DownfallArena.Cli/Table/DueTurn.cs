namespace DownfallArena.Cli.Table;

/// <summary>
/// "Your turn", owed to a seat once the grace is over (ADR 0118, 0203), as the notices queue holds it: the table, the
/// seat, the asking it is owed for and the round it said. Hidden on the queue for the grace, taken back when the
/// seat's page draws the question in time, and checked against the table when it comes due, so a seat that answered
/// meanwhile, or a match that ended, is told nothing.
/// </summary>
internal sealed record DueTurn(string Table, string Seat, long Asked, int? Round);
