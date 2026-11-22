namespace DownfallArena.Application.Accounts;

/// <summary>
/// What a notice to an account is about (notifications plan, ADR 0201). Four kinds wait for the player and the bell
/// already lists them from what waits; the other four are news, kept in the account's notice log, since nothing
/// else would show them once their push is missed. The names are the wire's (<see cref="NoticeKinds.Name" />).
/// </summary>
public enum NoticeKind
{
    /// <summary>Your turn, or your turn about to time out.</summary>
    Turn,

    /// <summary>A friend invites you to a table.</summary>
    Invitation,

    /// <summary>A player asks to be your friend.</summary>
    FriendRequest,

    /// <summary>A friend declined your invitation.</summary>
    Declined,

    /// <summary>A player accepted your friend request.</summary>
    FriendAccepted,

    /// <summary>The friend you invited took the seat.</summary>
    SeatTaken,

    /// <summary>The other player left the table.</summary>
    Left,

    /// <summary>A match ended: won, lost, a draw, voided.</summary>
    MatchOver,
}
