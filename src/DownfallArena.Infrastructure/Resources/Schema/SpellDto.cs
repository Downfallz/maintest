using System.Text.Json.Serialization;

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

    /// <summary>
    /// What the cast does to whoever cast it, once per cast (ADR 0031). Almost every spell has none, and a
    /// spell that has none is written without the field: an empty list is not content, so adding this must
    /// not move the content hash of a catalogue that does not use it.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<EffectDto>? CasterEffects { get; init; }

    /// <summary>
    /// Authoring-only switch (ADR 0015): <c>false</c> keeps the item out of the consolidated schema. The builder
    /// clears it, so the flag never reaches <c>game.schema.json</c> and never moves the content hash.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }
}
