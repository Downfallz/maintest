using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>The seats of a stepped table as it is opened.</summary>
/// <param name="Plan">Who the table was asked to seat, before any change its journal holds.</param>
/// <param name="Seats">The two seats as the host knows them, each with its <see cref="SteppedPerson" /> when a person holds it.</param>
/// <param name="Named">Who a name written in the journal stands for at a seat, the way the pilot's own seating says; none when nobody of that name can sit there.</param>
internal sealed record SteppedSeats(SeatPlan Plan, IReadOnlyList<TableSeat> Seats, Func<PlayerSlot, string, Occupant?> Named)
{
    /// <summary>The dice the table's bots throw, told where the journal ends as each of their turns begins; none where no bot throws any.</summary>
    public TableDice? Dice { get; init; }
}
