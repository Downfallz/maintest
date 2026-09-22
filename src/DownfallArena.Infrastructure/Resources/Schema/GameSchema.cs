using System.Text.Json.Serialization;

namespace DownfallArena.Infrastructure.Resources.Schema;

/// <summary>
/// The consolidated game content produced by the data builder (ADR 0009). Every reference is a versioned id.
/// </summary>
public sealed record GameSchema
{
    /// <summary>
    /// The last version whose spells carried a per-spell <c>initiative</c>. 1 had no <see cref="Tiers"/> member
    /// and 2 carried one; both are unreadable here, because ADR 0059 removed that member from a spell and the
    /// content hash is taken over this document.
    /// </summary>
    public const int LastVersionWithASpellInitiative = 2;

    /// <summary>The document without a <see cref="Tiers"/> member, spells as ADR 0059 left them.</summary>
    public const int VersionWithoutTiers = 3;

    /// <summary>The document that carries <see cref="Tiers"/>, which a reader written before them cannot read.</summary>
    public const int VersionWithTiers = 4;

    /// <summary>
    /// The lowest version that can read this document, not the newest the builder knows. A catalogue with no
    /// packages stays at <see cref="VersionWithoutTiers"/>; one that has them says so, because a reader that
    /// predates the member refuses it as an unknown field.
    /// <para>
    /// Both numbers moved at ADR 0059, which took the per-spell <c>initiative</c> out of a spell. Dropping a
    /// member is the same compatibility break as adding one and in the same place: the hash is taken over this
    /// document, so a reader that still knows the member deserializes it back as 0, writes it into the
    /// canonical form, and reports a hash that does not match — corruption, for a catalogue that is sound. A
    /// version it does not know is the sentence worth reading instead.
    /// </para>
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
