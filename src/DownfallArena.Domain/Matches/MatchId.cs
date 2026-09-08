namespace DownfallArena.Domain.Matches;

/// <summary>
/// Strongly typed identifier for a <c>Match</c>. Placeholder for the Matches bounded context.
/// </summary>
public readonly record struct MatchId(Guid Value)
{
    public static MatchId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
