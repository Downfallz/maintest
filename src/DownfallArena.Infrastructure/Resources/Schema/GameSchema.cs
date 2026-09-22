using System.Text.Json.Serialization;

namespace DownfallArena.Infrastructure.Resources.Schema;

/// <summary>
/// The consolidated game content produced by the data builder (ADR 0009). Every reference is a versioned id.
/// </summary>
public sealed record GameSchema
{
    /// <summary>The document as it was before evolution packages existed: no <see cref="Tiers"/> member.</summary>
    public const int VersionWithoutTiers = 1;

    /// <summary>The document that carries <see cref="Tiers"/>, which a reader written before them cannot read.</summary>
    public const int VersionWithTiers = 2;

    /// <summary>
    /// The lowest version that can read this document, not the newest the builder knows. A catalogue with no
    /// packages stays at <see cref="VersionWithoutTiers"/> because it is still exactly the document that version
    /// always was, down to its content hash; one that has them says so, because a reader that predates the
    /// member refuses it as an unknown field.
    /// </summary>
    public int SchemaVersion { get; init; } = VersionWithoutTiers;

    /// <summary>
    /// SHA-256 of this document serialised with an empty hash, so the content can be verified after loading.
    /// </summary>
    public string ContentHash { get; init; } = string.Empty;

    public IReadOnlyList<CreatureDefinitionDto> Creatures { get; init; } = [];

    public IReadOnlyList<SpellDto> Spells { get; init; } = [];

    public IReadOnlyList<TalentTreeDto> TalentTrees { get; init; } = [];

    /// <summary>
    /// The packages one evolution pick buys, absent rather than empty in a catalogue that has none. The hash is
    /// taken over this document, so an empty member would rehash every catalogue authored before packages
    /// existed and make <c>Load</c> reject it; the same reason ADR 0031 drops an empty caster-effect list.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TierDto>? Tiers { get; init; }

    public IReadOnlyDictionary<string, string> Aliases { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
