using DA.Game.Resources.Dto;

namespace DA.Game.Infrastructure.Bootstrap;
public sealed record GameSchema
{
    public int SchemaVersion { get; init; } = 1;
    public required IReadOnlyList<SpellDefinitionDto> Spells { get; init; }
    public required IReadOnlyList<CreatureDefinitionDto> Creatures { get; init; }
    public Dictionary<string, string>? Aliases { get; init; }
    public required string BuildHash { get; init; }
}
