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

    /// <summary>A player conceded; the other player won on the spot (ADR 0087).</summary>
    Concession,

    /// <summary>
    /// A player asked to decide let the table's time to answer run out (ADR 0169): the other player won, or, when
    /// both were asked and both let it run out, nobody did.
    /// </summary>
    TimedOut,
}
