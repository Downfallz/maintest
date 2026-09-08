namespace DownfallArena.Infrastructure.Resources.Schema;

public sealed record TalentTreeDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public TalentNodeDto? Root { get; init; }
}
