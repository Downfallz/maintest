namespace DownfallArena.Infrastructure.Resources.Schema;

public sealed record CreatureDefinitionDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string CreatureClass { get; init; } = string.Empty;

    public int BaseHealth { get; init; }

    public int BaseEnergy { get; init; }

    public int BaseDefense { get; init; }

    public int BaseInitiative { get; init; }

    public double BaseCriticalChance { get; init; }

    public string TalentTreeId { get; init; } = string.Empty;

    public IReadOnlyList<string> StartingSpellIds { get; init; } = [];
}
