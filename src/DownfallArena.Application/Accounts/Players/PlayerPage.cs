namespace DownfallArena.Application.Accounts.Players;

/// <summary>
/// One page of the list of players: its rows, how many accounts there are, how many match the search, and which page
/// of how many this is, counted from one.
/// </summary>
public sealed record PlayerPage(IReadOnlyList<PlayerSummary> Players, int Total, int Matching, int Page, int Pages);
