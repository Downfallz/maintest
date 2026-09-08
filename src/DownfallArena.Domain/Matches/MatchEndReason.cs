namespace DownfallArena.Domain.Matches;

/// <summary>
/// Why a match ended (ADR 0011).
/// </summary>
public enum MatchEndReason
{
    /// <summary>At least one team had no living creature at the end of a round.</summary>
    Elimination,

    /// <summary>The rule set's round cap was reached; total remaining health decided.</summary>
    RoundCap,
}
