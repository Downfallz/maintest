namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// What a player is expected to do right now. Exactly one section of <see cref="PlayerOptions"/> is filled
/// for the kinds that take a decision.
/// </summary>
public enum PlayerOptionsKind
{
    /// <summary>Nothing to do: the match has not started, the other player is up, or a step is automatic.</summary>
    Waiting,

    Evolution,

    Speed,

    /// <summary>The player orders their own tied creatures among the places their side won (ADR 0063).</summary>
    TieOrder,

    Intent,

    /// <summary>
    /// The player's creature whose slot has come up chooses its targets; the action resolves on confirmation
    /// (ADR 0083).
    /// </summary>
    Target,

    Ended,
}
