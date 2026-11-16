namespace DownfallArena.Application.Accounts.Players;

/// <summary>
/// What the list of players is ordered by. A name reads A to Z; every other column puts the most first: the newest
/// account, the latest sign-in or visit, the most games, the highest rating.
/// </summary>
public enum PlayerSort
{
    Seen,
    SignedIn,
    Created,
    Name,
    Games,
    Rating,
}
