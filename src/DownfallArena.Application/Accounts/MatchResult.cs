namespace DownfallArena.Application.Accounts;

/// <summary>
/// One finished match a person with an account played (ADR 0122, stage 5): what a ladder will be read from. It
/// names the accounts by their internal id and never by name, so a rename or a deleted account changes nothing
/// here. <see cref="Winner" /> is the winning seat's name, or null for a draw.
/// </summary>
public sealed record MatchResult(
    string MatchId,
    DateTimeOffset FinishedAt,
    string Format,
    string Declaration,
    string ContentHash,
    int Rounds,
    string? Winner,
    IReadOnlyList<ResultSeat> Seats)
{
    /// <summary>
    /// Whether the match counts on a ladder of people: two seats, each held by its own account. A bot, a seat
    /// nobody owned, or one account holding both seats (a shared device) makes it unranked.
    /// </summary>
    public bool Ranked =>
        Seats.Count == 2 && Seats.All(seat => seat.Account is not null) && Seats[0].Account != Seats[1].Account;
}
