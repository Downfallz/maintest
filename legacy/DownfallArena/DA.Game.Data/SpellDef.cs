namespace DA.Game.Data;

public sealed class SpellDef
{
    public required string Id { get; init; }              // "spell:frost_nova:v1"
    public int SchemaVersion { get; init; } = 1;          // change si la structure change
    public required string Name { get; init; }
    public required int EnergyCost { get; init; }
    public required int Initiative { get; init; }
    public required List<EffectDef> Effects { get; init; }
    public string? School { get; init; }
}

