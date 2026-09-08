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

    Intent,

    Target,

    /// <summary>A combat action waits for resolution; any host may drive it.</summary>
    Resolution,

    Ended,
}
