namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// Where a round begins in a Decision journal: the number of entries the match goes through before the round's
/// first question. Every entry before it belongs to an earlier round, none of this round's is among them.
/// </summary>
/// <param name="Round">The round, as the board numbers it.</param>
/// <param name="CutAt">How many of the journal's entries come before the round begins.</param>
public sealed record RoundCut(int Round, int CutAt);
