using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// A journaled match as it stands where its journal ends: what each seat reads, how it ended if it has, and
/// the seats that were told to change hands. Read off one turn, so the two seats are of the same moment.
/// </summary>
public sealed record JournaledState(
    MatchOutcome? Outcome,
    SeatReading Player1,
    SeatReading Player2,
    JournalPosition Position,
    IReadOnlyList<SeatChangeEntry> SeatChanges)
{
    public SeatReading Of(PlayerSlot slot) => slot == PlayerSlot.Player1 ? Player1 : Player2;
}
