using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Everything that reaches one seat (ADR 0116): the token its browser holds, the invitation a link or a code
/// carries while nobody has taken it, whether somebody has, the code itself, and where that browser's notices
/// go (ADR 0118), the account the seat belongs to (ADR 0122), and the account it is kept for while nobody has
/// taken it (ADR 0129). A fresh token has no subscription: it is a new browser's, or nobody's yet.
/// </summary>
internal sealed record SeatKeys(string Token, string? Invitation, bool Taken, string? Code = null, PushSubscription? Push = null, AccountId? Account = null, AccountId? InvitedAccount = null, DateTimeOffset? InvitedAt = null)
{
    /// <summary>A seat a browser holds, for the account signed in there if any: a fresh token, and nothing left for a link to name.</summary>
    public static SeatKeys TakenBy(string token, AccountId? account = null) => new(token, null, Taken: true, Account: account);

    /// <summary>A seat nobody holds: a fresh token nobody has, and a fresh invitation for a link and a code.</summary>
    public static SeatKeys Offered(string table) => new(TableKeys.For(table), TableKeys.For(table), Taken: false);

    /// <summary>A seat kept for a friend (ADR 0129): fresh keys nobody has, and no code or link, since only that account accepts.</summary>
    public static SeatKeys KeptFor(string table, AccountId friend, DateTimeOffset at) => new(TableKeys.For(table), TableKeys.For(table), Taken: false, InvitedAccount: friend, InvitedAt: at);
}
