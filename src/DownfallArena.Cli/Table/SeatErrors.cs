using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>Why a seat's request was refused by the table, where the match itself has nothing to say about it.</summary>
internal static class SeatErrors
{
    /// <summary>
    /// The seat's own person is not the one playing it: a bot holds it for them, until a handover or after a
    /// swap, and a match a bot is playing is not theirs to act on.
    /// </summary>
    public static readonly DomainError PlayedByABot = new("Seat.PlayedByABot", "This seat is played by a bot; it decides for itself.");
}
