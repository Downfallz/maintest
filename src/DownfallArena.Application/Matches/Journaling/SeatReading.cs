using DownfallArena.Application.Matches.Driving;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// What a seat reads of a journaled match, and which asking of the seat's its question is.
/// </summary>
/// <param name="Asking">
/// One more than the decisions the seat has in the journal. Two questions of the same shape -- two picks of one
/// opportunity -- are two askings, so an answer on its way to the first cannot be taken for the second; and the
/// other seat's answers do not move it.
/// </param>
public sealed record SeatReading(SeatView View, long Asking);
