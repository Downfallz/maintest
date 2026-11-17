namespace DownfallArena.Cli.Table;

/// <summary>
/// The line of the recap the lobby, the inbox and "Your turn" carry (async recap plan, R4): how many spells were
/// cast since the seat's last visit, and when the last move was. Counted from the seat's own feed after the mark
/// it was last shown (ADR 0194); no board is read.
/// </summary>
/// <param name="Casts">The casts the seat may see after its last visit; none when the seat has no last visit.</param>
/// <param name="LastMoveAt">When the journal was last written; none when it holds nothing or could not be read.</param>
internal sealed record SinceVisit(int? Casts, DateTimeOffset? LastMoveAt);
