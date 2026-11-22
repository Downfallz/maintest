namespace DownfallArena.Application.Accounts;

/// <summary>
/// What pops up on the account's devices (notifications plan, N3, ADR 0202): its turns, its invitations and friends,
/// its results. The bell keeps every notice whatever these say: they choose the push, never what is kept.
/// </summary>
public sealed record NoticePreferences(bool Turns, bool Invitations, bool Results)
{
    /// <summary>What an account that never chose gets: every kind pops up, as before the choice existed.</summary>
    public static NoticePreferences Default { get; } = new(Turns: true, Invitations: true, Results: true);

    /// <summary>
    /// Whether a notice of this kind pops up. A friend request, an accepted request, a declined invitation and a seat
    /// taken are the invitations and friends; the other player leaving and a match ending are the results.
    /// </summary>
    public bool Pops(NoticeKind kind) => kind switch
    {
        NoticeKind.Turn => Turns,
        NoticeKind.Invitation or NoticeKind.FriendRequest or NoticeKind.FriendAccepted or NoticeKind.Declined or NoticeKind.SeatTaken => Invitations,
        NoticeKind.Left or NoticeKind.MatchOver => Results,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of notice."),
    };
}
