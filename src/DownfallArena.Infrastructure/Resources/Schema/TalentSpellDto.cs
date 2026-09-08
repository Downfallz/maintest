namespace DownfallArena.Infrastructure.Resources.Schema;

public sealed record TalentSpellDto
{
    public string Id { get; init; } = string.Empty;

    public PrerequisitesDto? Prerequisites { get; init; }
}
