namespace DownfallArena.Application.Content;

/// <summary>
/// What the audit makes of one built content set: the content it looked at, what no creature can reach or cast,
/// and one row per spell to compare the rest with.
/// </summary>
public sealed record ContentAuditReport
{
    /// <summary>The content hash the audit ran on.</summary>
    public required string ContentVersion { get; init; }

    public required int Creatures { get; init; }

    public required int Spells { get; init; }

    public required int TalentTrees { get; init; }

    /// <summary>The round cap and energy per round the reachability was computed with.</summary>
    public required int RoundCap { get; init; }

    public required int EnergyPerRound { get; init; }

    public required IReadOnlyList<ContentFinding> Findings { get; init; }

    /// <summary>One row per spell, the widest reach first.</summary>
    public required IReadOnlyList<SpellReach> Reach { get; init; }
}
