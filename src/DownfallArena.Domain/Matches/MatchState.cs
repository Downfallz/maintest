namespace DownfallArena.Domain.Matches;

/// <summary>
/// Where a match is in its life: waiting for its two players, in progress, or ended.
/// </summary>
public enum MatchState
{
    WaitingForPlayers,
    InProgress,
    Ended,
}
