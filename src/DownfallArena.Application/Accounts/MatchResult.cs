namespace DownfallArena.Application.Accounts;

/// <summary>
/// One finished match a person with an account played (ADR 0122, stage 5): what a ladder will be read from. It
/// names the accounts by their internal id and never by name, so a rename or a deleted account changes nothing
/// here. <see cref="Winner" /> is the winning seat's name, or null for a draw. <see cref="Table" /> is the table
/// it was played at, whose recorded session keeps the match (ADR 0147); null in a result kept before it was.
/// <see cref="Casual" /> marks a game its players opened as unranked (ADR 0155): it is kept and listed, never rated.
/// <see cref="Teams" /> is what each seat played, for a player's statistics; empty in a result kept before it was.
/// </summary>
public sealed record MatchResult(
    string MatchId,
    DateTimeOffset FinishedAt,
    string Format,
    string Declaration,
    string ContentHash,
    int Rounds,
    string? Winner,
    IReadOnlyList<ResultSeat> Seats,
    string? Table = null,
    bool Casual = false)
{
    /// <summary>
    /// Why the operator voided the match, or null for a result that counts (ADR 0176). A void result is kept and listed
    /// among a player's finished games, and counts nowhere else: not rated, not in statistics, not among opponents.
    /// </summary>
    public MatchVoid? Voided { get; init; }

    /// <summary>Every creature both seats played, the first seat's first; empty in a result kept before it was.</summary>
    public IReadOnlyList<PlayedCreature> Teams { get; init; } = [];

    /// <summary>
    /// Whether the match counts on a ladder of people: two seats, each held by its own account, in a game not opened
    /// as unranked. A bot, a seat nobody owned, one account holding both seats (a shared device), or a casual game
    /// makes it unranked.
    /// </summary>
    public bool Ranked =>
        !Casual && Voided is null && Seats.Count == 2 && Seats.All(seat => seat.Account is not null) && Seats[0].Account != Seats[1].Account;

    /// <summary>Whether a bot held a seat: the lobby lists these apart from games between people (ADR 0147).</summary>
    public bool AgainstBot => Seats.Any(seat => seat.Agent is not null);
}
