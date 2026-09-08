namespace DownfallArena.Infrastructure.Resources.Schema;

public sealed record SpellDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string SpellType { get; init; } = string.Empty;

    public string CreatureClass { get; init; } = string.Empty;

    public int Initiative { get; init; }

    public int EnergyCost { get; init; }

    public double CriticalChance { get; init; }

    public TargetingDto? Targeting { get; init; }

    public IReadOnlyList<EffectDto> Effects { get; init; } = [];
}
