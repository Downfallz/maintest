using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// A seat changing hands from the top of a round. It changes nothing in the match -- every decision is written
/// whoever took it -- and everything about who is asked next, so it is kept where the decisions are.
/// </summary>
/// <param name="To">Who was named for the seat, as whoever asked named them; what that names is the host's to say.</param>
/// <param name="FromRound">The round the seat changes hands at the top of.</param>
/// <param name="Reached">
/// The last round the seat had decided anything in when this was asked, zero before its first decision. A seat
/// waits to make one change at a time: a change asked while another had not happened yet replaces it, and this
/// is what says which had happened. None in a line written before the journal was the source (ADR 0159).
/// </param>
public sealed record SeatChangeEntry(PlayerSlot Slot, string To, int FromRound, int? Reached = null) : IJournalEntry;
