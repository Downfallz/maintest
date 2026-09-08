namespace DownfallArena.Infrastructure.Resources.Schema;

public sealed record TargetingDto
{
    public string Origin { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public int? MaxTargets { get; init; }
}
