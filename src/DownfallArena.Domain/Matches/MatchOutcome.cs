namespace DownfallArena.Domain.Matches;

/// <summary>
/// How a match ended: the winning player slot, or <c>null</c> for a draw, and the reason.
/// </summary>
public sealed record MatchOutcome(PlayerSlot? Winner, MatchEndReason Reason)
{
    public bool IsDraw => Winner is null;
}
