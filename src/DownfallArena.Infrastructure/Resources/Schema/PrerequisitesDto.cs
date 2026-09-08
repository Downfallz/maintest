namespace DownfallArena.Infrastructure.Resources.Schema;

public sealed record PrerequisitesDto
{
    public IReadOnlyList<string> AllOf { get; init; } = [];

    public IReadOnlyList<string> AnyOf { get; init; } = [];
}
