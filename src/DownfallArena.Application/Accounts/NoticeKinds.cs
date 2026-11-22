namespace DownfallArena.Application.Accounts;

/// <summary>How a <see cref="NoticeKind" /> is written and whether the bell keeps it as news (ADR 0201).</summary>
public static class NoticeKinds
{
    /// <summary>The kind as a page and a stored log read it: <c>friend-request</c>, <c>match-over</c>.</summary>
    public static string Name(NoticeKind kind) => kind switch
    {
        NoticeKind.Turn => "turn",
        NoticeKind.Invitation => "invitation",
        NoticeKind.FriendRequest => "friend-request",
        NoticeKind.Declined => "declined",
        NoticeKind.FriendAccepted => "friend-accepted",
        NoticeKind.SeatTaken => "seat-taken",
        NoticeKind.Left => "left",
        NoticeKind.MatchOver => "match-over",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of notice."),
    };

    /// <summary>The kind a name says, or none for a name no kind has.</summary>
    public static NoticeKind? Parse(string? name) =>
        Enum.GetValues<NoticeKind>().Cast<NoticeKind?>().FirstOrDefault(kind => string.Equals(Name(kind!.Value), name, StringComparison.Ordinal));

    /// <summary>
    /// Whether the notice is news: something that happened and calls for nothing. What waits is listed by the bell
    /// from the lobby and the friends while it waits, so the log keeps only the rest.
    /// </summary>
    public static bool IsNews(NoticeKind kind) => kind is NoticeKind.FriendAccepted or NoticeKind.SeatTaken or NoticeKind.Left or NoticeKind.MatchOver;
}
