namespace DownfallArena.Cli.Table;

/// <summary>Who a seat is waiting to hand itself to, and the round it happens at the top of.</summary>
/// <param name="Round">
/// The round the new occupant plays from. A swap never lands inside a round: the driver asks one seat for
/// several decisions within a sub-phase, so a seat that changed hands between two of them would split that
/// sub-phase between two players.
/// </param>
internal sealed record Swap(Occupant Next, int Round);
