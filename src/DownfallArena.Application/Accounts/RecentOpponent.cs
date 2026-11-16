using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// A player an account has played against, as its kept results tell it: when they last played, how the games
/// between them went from this account's side, and the format and rating of the last one, which a challenge
/// starts from.
/// </summary>
public sealed record RecentOpponent(
    AccountId Account,
    DateTimeOffset LastPlayed,
    int Games,
    int Won,
    int Lost,
    int Drawn,
    string LastFormat,
    bool LastCasual);
