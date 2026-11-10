using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One seat as the host knows it: which player it is, the token that proves a request is its own, and the
/// person waiting in it — or none, when a bot is playing it.
/// </summary>
/// <remarks>
/// The token is what makes the boundary a boundary. Both players share one screen in hotseat, so the browser
/// holds both tokens and nothing stops it asking for the other seat; what the token buys is that the *server*
/// never answers two seats in one payload, and that two devices later is a change of who holds which token
/// rather than a change of what the host serves (<c>docs/tabletop/playtest-app.md</c> §4).
/// </remarks>
internal sealed record TableSeat(PlayerSlot Slot, string Token, ISeatPerson? Person)
{
    /// <summary>Replaced when its player explicitly leaves, revoking their previous access.</summary>
    public string Token { get; set; } = Token;

    /// <summary>
    /// The single-use key a link and a code carry while nobody has taken this person's seat (ADR 0116). It is
    /// not the seat's token: the host trades it for the token once, to the first browser that confirms, and
    /// forgets it, so a second browser opening the same link finds the place taken rather than a second key to
    /// the same seat. Null once taken, for a bot's seat, and for a seat recorded before invitations existed.
    /// </summary>
    public string? Invitation { get; set; }

    /// <summary>Whether a browser holds this seat's token: it took the invitation, or the lobby handed it over.</summary>
    public bool Taken { get; set; }

    /// <summary>
    /// Where the browser holding this seat asked its notices to be sent (ADR 0118), or none. One of the seat's
    /// keys: it goes when the token does, since it is that browser's and not the next one's.
    /// </summary>
    public PushSubscription? Push { get; set; }

    /// <summary>
    /// The account this seat belongs to (ADR 0122), or none: a bot's seat, one nobody has taken, or one taken on a
    /// host without accounts. One of the seat's keys, so it goes when the token does: leaving gives the place back.
    /// </summary>
    public AccountId? Account { get; set; }

    /// <summary>
    /// The account this seat is kept for (ADR 0129), or none: the friend the table was opened for, until they
    /// accept, decline, the inviter cancels, or the invitation runs out. While set, the seat is nobody else's to
    /// take: not an open place, no code, no link. One of the seat's keys, so taking the seat clears it.
    /// </summary>
    public AccountId? InvitedAccount { get; set; }

    /// <summary>When the invited account was asked, which is what the invitation's expiry is counted from.</summary>
    public DateTimeOffset? InvitedAt { get; set; }

    /// <summary>
    /// The account that declined this seat's invitation, or none: what the inviter's card says while the seat
    /// waits for the inviter to choose a way out (ADR 0129). Cleared by the next invitation, by opening the seat
    /// to everyone, and when the seat is taken.
    /// </summary>
    public AccountId? DeclinedBy { get; set; }

    /// <summary>
    /// What a link or a code hands out for this seat: its invitation, or, for a seat recorded before invitations
    /// existed, its token, as links did then. Nothing once the seat is taken, nothing for a bot's, and nothing
    /// while the seat is kept for a friend (ADR 0129): that invitation is accepted signed in, never by a key.
    /// </summary>
    public string? Key => Person is null || Taken || InvitedAccount is not null ? null : Invitation ?? Token;

    /// <summary>The name a URL and a page use for a slot.</summary>
    public static string NameOf(PlayerSlot slot) => slot == PlayerSlot.Player1 ? "player1" : "player2";

    /// <summary>The slot a URL segment names, or <c>null</c> when it names neither.</summary>
    public static PlayerSlot? SlotOf(string name) => name switch
    {
        "player1" => PlayerSlot.Player1,
        "player2" => PlayerSlot.Player2,
        _ => null,
    };

    public string Name => NameOf(Slot);
}
