namespace DA.Game.Data;

public sealed class CreatureDef
{
    public required string Id { get; init; }              // "creature:mage:v1"
    public required string Name { get; init; }
    public int BaseHealth { get; init; } = 20;
    public int BaseEnergy { get; init; } = 4;
    public required List<string> StartingSpellIds { get; init; }
}

