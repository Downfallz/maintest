using System.Text.Json;
using DownfallArena.Application.Catalogue;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources.Schema;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Resources;

/// <summary>
/// How each package shows on the creature that bought it in the arena (ADR 0133): the family whose body it
/// belongs to and the accent it draws, keyed by the package's versioned id. Presentation, not content: kept out of
/// the consolidated schema, so authoring it moves neither the content hash nor a benchmark digest, and kept out
/// of the page, so a content pass that adds a package adds its picture here and changes no page
/// (docs/tabletop/app-roadmap.md, stage 3). Authored in <c>data/presentation</c>, checked and published next to
/// the schema by the data builder, and read from there by the table host, the way the package tags and the spell
/// art are.
/// </summary>
public sealed class MutationArt
{
    /// <summary>Where it is authored, under the content directory.</summary>
    public static readonly string SourceFile = Path.Combine("presentation", "mutation-art.json");

    /// <summary>What it is published as, next to the consolidated schema.</summary>
    public const string FileName = "mutation-art.json";

    /// <summary>The three bodies of the art direction, in the order a tie between them is broken.</summary>
    public static readonly IReadOnlyList<string> Families = ["brute", "predator", "warped"];

    /// <summary>The accents the arena knows how to draw on a body; a root package draws none.</summary>
    public static readonly IReadOnlyList<string> Motifs =
        ["fissures", "plates", "crown", "talons", "fangs", "glands", "shards", "souls", "astral", "reinforcement", "precision", "circuit"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IReadOnlyDictionary<TierId, MutationVisual> _visuals;

    private MutationArt(IReadOnlyDictionary<TierId, MutationVisual> visuals) => _visuals = visuals;

    /// <summary>No art at all: a catalogue published before it, or by a builder that had none.</summary>
    public static MutationArt None { get; } = new(new Dictionary<TierId, MutationVisual>());

    /// <summary>A package's visual, or <c>null</c> for one nobody drew: the creature then shows no trace of it.</summary>
    public MutationVisual? Of(TierId package) => _visuals.TryGetValue(package, out var visual) ? visual : null;

    /// <summary>The art published next to a schema, or none when the builder that wrote it published none.</summary>
    public static MutationArt ReadNextTo(string schemaPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);
        var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(schemaPath)) ?? ".", FileName);
        if (!File.Exists(path))
        {
            return None;
        }

        var authored = JsonSerializer.Deserialize<Dictionary<string, AuthoredVisual>>(File.ReadAllText(path), JsonOptions) ?? [];
        return new MutationArt(authored.ToDictionary(entry => TierId.Parse(entry.Key), entry => entry.Value.ToVisual()));
    }

    /// <summary>
    /// Checks the authored art against the authored packages and the vocabularies, and publishes that of the
    /// schema's packages next to it. Content that authors none publishes none. A package the content does not
    /// author, a family or a motif outside its list, a root package with a motif, a branch without one, or a
    /// package whose family is not the family of the root it descends from, is refused with every problem at
    /// once, as the rest of the content is. A package without art is not a problem: the creature shows no trace of
    /// it. A disabled package is authored: its art is checked and left out of what is published.
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
            throw new InvalidGameContentException($"{SourceFile}: not a map of package ids to a family and a motif ({exception.Message}).");
        }

        var tiers = Authored(dataDirectory, schema).Concat(schema.Tiers ?? []).GroupBy(tier => tier.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var problems = authored.SelectMany(entry => Problems(entry.Key, entry.Value, tiers, authored)).ToList();
        if (problems.Count > 0)
        {
            throw new InvalidGameContentException(problems);
        }

        var built = (schema.Tiers ?? []).Select(tier => tier.Id).ToHashSet(StringComparer.Ordinal);
        var published = authored.Where(entry => built.Contains(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, FileName), JsonSerializer.Serialize(published, JsonOptions));
    }

    /// <summary>
    /// Every package the content authors, the disabled ones included (ADR 0015): the switch takes a package out of
    /// the schema, not out of the content, so its art is still the art of a package.
    /// </summary>
    private static IEnumerable<TierDto> Authored(string dataDirectory, GameSchema schema)
    {
        var folder = Path.Combine(dataDirectory, GameSchemaBuilder.TiersFolder);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .Select(file => JsonSerializer.Deserialize<TierDto>(File.ReadAllText(file), GameSchemaJson.ReadOptions))
            .OfType<TierDto>()
            .Select(tier => schema.Aliases.TryGetValue(tier.Id, out var current) ? tier with { Id = current } : tier);
    }

    private static IEnumerable<string> Problems(string package, AuthoredVisual visual, Dictionary<string, TierDto> tiers, Dictionary<string, AuthoredVisual> authored)
    {
        if (!tiers.TryGetValue(package, out var tier))
        {
            yield return $"{SourceFile}: '{package}' is no package this content authors.";
            yield break;
        }

        if (!Families.Contains(visual.Family, StringComparer.Ordinal))
        {
            yield return $"{SourceFile}: '{package}' is of the family '{visual.Family}', which is none of {string.Join(", ", Families)}.";
        }

        if (tier.Level == 1 && visual.Motif is not null)
        {
            yield return $"{SourceFile}: '{package}' is a root and draws '{visual.Motif}'; a root is the body itself and draws no accent.";
        }

        if (tier.Level > 1 && visual.Motif is null)
        {
            yield return $"{SourceFile}: '{package}' draws no accent; a package above the root draws one of {string.Join(", ", Motifs)}.";
        }

        if (visual.Motif is not null && !Motifs.Contains(visual.Motif, StringComparer.Ordinal))
        {
            yield return $"{SourceFile}: '{package}' draws '{visual.Motif}', which is none of {string.Join(", ", Motifs)}.";
        }

        // A body is the root's: a branch drawn on another family's body would move a creature's silhouette with no
        // purchase to show for it.
        var root = RootOf(tier, tiers);
        if (root is not null && root != package && authored.TryGetValue(root, out var rootVisual) && rootVisual.Family != visual.Family)
        {
            yield return $"{SourceFile}: '{package}' is of the family '{visual.Family}' but descends from '{root}', which is of the family '{rootVisual.Family}'.";
        }
    }

    /// <summary>The root a package descends from, through its first prerequisite or, for a capstone, any of the packages that open it; <c>null</c> when the chain leaves the content.</summary>
    private static string? RootOf(TierDto tier, Dictionary<string, TierDto> tiers)
    {
        var current = tier;
        for (var depth = 0; depth < 8; depth++)
        {
            if (current.Level <= 1)
            {
                return current.Id;
            }

            var parent = current.Prerequisites.Count > 0 ? current.Prerequisites[0] : null;
            if (parent is null && current.AnyOf is { Count: > 0 })
            {
                parent = current.AnyOf[0];
            }

            if (parent is null || !tiers.TryGetValue(parent, out var next))
            {
                return null;
            }

            current = next;
        }

        return null;
    }

    /// <summary>The authored shape: the family, the empty string when left out so it is refused by name, and the motif, absent for a root.</summary>
    private sealed record AuthoredVisual
    {
        public string Family { get; init; } = string.Empty;

        public string? Motif { get; init; }

        public MutationVisual ToVisual() => new(Family, Motif);
    }
}
