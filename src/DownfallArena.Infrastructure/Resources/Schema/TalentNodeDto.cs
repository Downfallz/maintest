namespace DownfallArena.Infrastructure.Resources.Schema;

public sealed record TalentNodeDto
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public PrerequisitesDto? Prerequisites { get; init; }

    public IReadOnlyList<TalentSpellDto> Spells { get; init; } = [];

    public IReadOnlyList<TalentNodeDto> Children { get; init; } = [];
}
