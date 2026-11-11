namespace DownfallArena.Application.Accounts;

/// <summary>How a player's games of one format went.</summary>
public sealed record FormatTally(string Format, Tally Tally);
