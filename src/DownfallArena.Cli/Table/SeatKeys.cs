namespace DownfallArena.Cli.Table;

/// <summary>
/// Everything that reaches one seat (ADR 0116): the token its browser holds, the invitation a link or a code
/// carries while nobody has taken it, whether somebody has, and the code itself.
/// </summary>
internal sealed record SeatKeys(string Token, string? Invitation, bool Taken, string? Code = null)
{
    /// <summary>A seat a browser holds: a fresh token, and nothing left for a link to name.</summary>
    public static SeatKeys TakenBy(string token) => new(token, null, Taken: true);

    /// <summary>A seat nobody holds: a fresh token nobody has, and a fresh invitation for a link and a code.</summary>
    public static SeatKeys Offered() => new(TableComposer.Token(), TableComposer.Token(), Taken: false);
}
