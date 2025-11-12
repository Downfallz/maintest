namespace DA.Game.Data;

public sealed class GameSchema
{
    public int SchemaVersion { get; init; } = 1;
    public required List<SpellDef> Spells { get; init; }
    public required List<CreatureDef> Creatures { get; init; }
    public Dictionary<string, string>? Aliases { get; init; }
    public string? BuildHash { get; init; }
}

