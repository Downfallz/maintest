namespace DownfallArena.Infrastructure.Resources.Schema;

/// <summary>
/// The consolidated game content produced by the data builder (ADR 0009). Every reference is a versioned id.
/// </summary>
public sealed record GameSchema
{
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// SHA-256 of this document serialised with an empty hash, so the content can be verified after loading.
    /// </summary>
    public string ContentHash { get; init; } = string.Empty;

    public IReadOnlyList<CreatureDefinitionDto> Creatures { get; init; } = [];

    public IReadOnlyList<SpellDto> Spells { get; init; } = [];

    public IReadOnlyList<TalentTreeDto> TalentTrees { get; init; } = [];

    public IReadOnlyDictionary<string, string> Aliases { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
