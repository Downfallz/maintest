using System.Text.Json;
using DownfallArena.Application.Catalogue;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources.Schema;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Resources;

/// <summary>
/// How each spell's confirmed cast is drawn in the arena (ADR 0131): a family, a motif and a delivery, keyed by
/// the spell's versioned id. Presentation, not content: kept out of the consolidated schema, so authoring it
/// moves neither the content hash nor a benchmark digest, and kept out of the page, so a content pass that adds a
/// spell adds its picture here and changes no page (docs/tabletop/app-roadmap.md, stage 3). Authored in
/// <c>data/presentation</c>, checked and published next to the schema by the data builder, and read from there by
/// the table host, the way the package tags are.
/// </summary>
public sealed class SpellArt
{
    /// <summary>Where it is authored, under the content directory.</summary>
    public static readonly string SourceFile = Path.Combine("presentation", "spell-art.json");

    /// <summary>What it is published as, next to the consolidated schema.</summary>
    public const string FileName = "spell-art.json";

    /// <summary>The families a cast is coloured by: the five of the art direction, and neutral for what has none.</summary>
    public static readonly IReadOnlyList<string> Families =
        ["physical", "biological", "elemental", "necromantic", "astral", "neutral"];

    /// <summary>The shapes the arena knows how to draw; a motif outside the list would fall to a plain mark.</summary>
    public static readonly IReadOnlyList<string> Motifs =
        ["impact", "slash", "bite", "quill", "wave", "roar", "plates", "spores", "arc", "fire", "ice", "bones", "spirits", "void", "vital", "focus"];

    /// <summary>Whether the caster closes in, sends something, or casts from where it stands.</summary>
    public static readonly IReadOnlyList<string> Deliveries = ["melee", "projectile", "cast"];

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    private readonly IReadOnlyDictionary<SpellId, SpellVisual> _visuals;

    private SpellArt(IReadOnlyDictionary<SpellId, SpellVisual> visuals) => _visuals = visuals;

    /// <summary>No art at all: a catalogue published before it, or by a builder that had none.</summary>
    public static SpellArt None { get; } = new(new Dictionary<SpellId, SpellVisual>());

    /// <summary>A spell's visual, or <c>null</c> for one nobody drew: the page then shows a neutral mark.</summary>
    public SpellVisual? Of(SpellId spell) => _visuals.TryGetValue(spell, out var visual) ? visual : null;

    /// <summary>The art published next to a schema, or none when the builder that wrote it published none.</summary>
    public static SpellArt ReadNextTo(string schemaPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);
        var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(schemaPath)) ?? ".", FileName);
        if (!File.Exists(path))
        {
            return None;
        }

        var authored = JsonSerializer.Deserialize<Dictionary<string, AuthoredVisual>>(File.ReadAllText(path), JsonOptions) ?? [];
        return new SpellArt(authored.ToDictionary(entry => SpellId.Parse(entry.Key), entry => entry.Value.ToVisual()));
    }

    /// <summary>
    /// Checks the authored art against the authored spells and the vocabularies, and publishes that of the
    /// schema's spells next to it. Content that authors none publishes none. A spell the content does not author,
    /// or a family, motif or delivery outside its list, is refused with every problem at once, as the rest of the
    /// content is. A spell without art is not a problem: the page draws a neutral mark for it. A disabled spell is
    /// authored: its art is checked and left out of what is published.
    /// </summary>
    public static void Publish(string dataDirectory, string outputDirectory, GameSchema schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(schema);

        var source = Path.Combine(dataDirectory, SourceFile);
        if (!File.Exists(source))
        {
            return;
        }

        Dictionary<string, AuthoredVisual> authored;
        try
        {
            authored = JsonSerializer.Deserialize<Dictionary<string, AuthoredVisual>>(File.ReadAllText(source), JsonOptions) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidGameContentException($"{SourceFile}: not a map of spell ids to a family, a motif and a delivery ({exception.Message}).");
        }

        var built = schema.Spells.Select(spell => spell.Id).ToHashSet(StringComparer.Ordinal);
        var spells = Authored(dataDirectory, schema).Union(built).ToHashSet(StringComparer.Ordinal);
        var problems = authored.SelectMany(entry => Problems(entry.Key, entry.Value, spells)).ToList();
        if (problems.Count > 0)
        {
            throw new InvalidGameContentException(problems);
        }

        var published = authored.Where(entry => built.Contains(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, FileName), JsonSerializer.Serialize(published, JsonOptions));
    }

    /// <summary>
    /// Every spell the content authors, the disabled ones included (ADR 0015): the switch takes a spell out of the
    /// schema, not out of the content, so its art is still the art of a spell.
    /// </summary>
    private static IEnumerable<string> Authored(string dataDirectory, GameSchema schema)
    {
        var folder = Path.Combine(dataDirectory, GameSchemaBuilder.SpellsFolder);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .Select(file => JsonSerializer.Deserialize<SpellDto>(File.ReadAllText(file), GameSchemaJson.ReadOptions)?.Id)
            .OfType<string>()
            .Select(id => schema.Aliases.TryGetValue(id, out var current) ? current : id);
    }

    private static IEnumerable<string> Problems(string spell, AuthoredVisual visual, HashSet<string> spells)
    {
        if (!spells.Contains(spell))
        {
            yield return $"{SourceFile}: '{spell}' is no spell this content authors.";
        }

        if (!Families.Contains(visual.Family, StringComparer.Ordinal))
        {
            yield return $"{SourceFile}: '{spell}' is of the family '{visual.Family}', which is none of {string.Join(", ", Families)}.";
        }

        if (!Motifs.Contains(visual.Motif, StringComparer.Ordinal))
        {
            yield return $"{SourceFile}: '{spell}' is drawn as '{visual.Motif}', which is none of {string.Join(", ", Motifs)}.";
        }

        if (!Deliveries.Contains(visual.Delivery, StringComparer.Ordinal))
        {
            yield return $"{SourceFile}: '{spell}' is delivered as '{visual.Delivery}', which is none of {string.Join(", ", Deliveries)}.";
        }
    }

    /// <summary>The authored shape: three words, each the empty string when the author left it out, so it is refused by name.</summary>
    private sealed record AuthoredVisual
    {
        public string Family { get; init; } = string.Empty;

        public string Motif { get; init; } = string.Empty;

        public string Delivery { get; init; } = string.Empty;

        public SpellVisual ToVisual() => new(Family, Motif, Delivery);
    }
}
