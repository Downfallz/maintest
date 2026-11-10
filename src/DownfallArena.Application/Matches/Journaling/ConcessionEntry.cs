using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// A seat giving the match up (ADR 0087). It ends the match and is no decision the options offer, so it is a
/// line of its own: a match rebuilt from its journal is conceded where it was.
/// </summary>
public sealed record ConcessionEntry(PlayerSlot Slot) : IJournalEntry;
