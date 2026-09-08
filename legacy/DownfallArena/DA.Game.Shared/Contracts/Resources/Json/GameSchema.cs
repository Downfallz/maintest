namespace DA.Game.Shared.Contracts.Resources.Json;

public sealed record GameSchema
{
    public int SchemaVersion { get; init; } = 1;
    public required IReadOnlyList<SpellDefinitionDto> Spells { get; init; }
    public required IReadOnlyList<CreatureDefinitionDto> Creatures { get; init; }
    public required IReadOnlyList<TalentTreeDefinitionDto> TalentTrees { get; init; }
    public Dictionary<string, string>? Aliases { get; init; }
    public required string BuildHash { get; init; }
}
