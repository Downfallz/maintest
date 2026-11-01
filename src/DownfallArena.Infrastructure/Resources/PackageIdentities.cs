using System.Text.Json;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources.Schema;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Resources;

/// <summary>
/// The two or three words each package is known by (docs/domain/package-identity-tags.md): the kind of build it
/// promises, for a player to scan before reading its spells. Presentation, not content: they are kept out of the
/// consolidated schema, so writing them moves neither the content hash nor a benchmark digest. They are authored
/// in <c>data/presentation</c>, checked and published next to the schema by the data builder, and read from there
/// by the table host.
/// </summary>
public sealed class PackageIdentities
{
    /// <summary>Where they are authored, under the content directory.</summary>
    public static readonly string SourceFile = Path.Combine("presentation", "package-tags.json");

    /// <summary>What they are published as, next to the consolidated schema.</summary>
    public const string FileName = "package-tags.json";

    /// <summary>The controlled vocabulary: a tag is one of these, never a near-synonym of one.</summary>
    public static readonly IReadOnlyList<string> Vocabulary =
        ["Damage", "Heal", "Pressure", "Burst", "AoE", "Control", "Defense", "Sustain", "Bleed", "Energy", "Drain", "Sacrifice", "Support", "Debuff", "Tempo", "Focus"];

    private const int Fewest = 2;

    private const int Most = 3;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly IReadOnlyDictionary<TierId, IReadOnlyList<string>> _tags;

    private PackageIdentities(IReadOnlyDictionary<TierId, IReadOnlyList<string>> tags) => _tags = tags;

    /// <summary>No tags at all: a catalogue published before them, or by a builder that had none.</summary>
    public static PackageIdentities None { get; } = new(new Dictionary<TierId, IReadOnlyList<string>>());

    /// <summary>A package's tags, or none: a capstone has none, and neither has a package nobody tagged.</summary>
    public IReadOnlyList<string> Of(TierId tier) => _tags.TryGetValue(tier, out var tags) ? tags : [];

    /// <summary>The tags published next to a schema, or none when the builder that wrote it published none.</summary>
    public static PackageIdentities ReadNextTo(string schemaPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);
        var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(schemaPath)) ?? ".", FileName);
        if (!File.Exists(path))
        {
            return None;
        }

        var authored = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(path)) ?? [];
        return new PackageIdentities(authored.ToDictionary(entry => TierId.Parse(entry.Key), entry => (IReadOnlyList<string>)entry.Value));
    }

    /// <summary>
    /// Checks the authored tags against the authored packages and the vocabulary, and publishes those of the
    /// schema's packages next to it. Content that authors none publishes none. A tag outside the vocabulary, a
    /// package with fewer than two or more than three, the same tag twice, or a package the content does not author
    /// is refused with every problem at once, as the rest of the content is. A disabled package is authored: its
    /// tags are checked and left out of what is published.
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

        var text = File.ReadAllText(source);
        Dictionary<string, string[]> authored;
        try
        {
            authored = JsonSerializer.Deserialize<Dictionary<string, string[]>>(text) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidGameContentException($"{SourceFile}: not a map of package ids to tags ({exception.Message}).");
        }

        var built = (schema.Tiers ?? []).Select(tier => tier.Id).ToHashSet(StringComparer.Ordinal);
        var packages = Authored(dataDirectory, schema).Union(built).ToHashSet(StringComparer.Ordinal);
        var problems = authored.SelectMany(entry => Problems(entry.Key, entry.Value, packages)).ToList();
        if (problems.Count > 0)
        {
            throw new InvalidGameContentException(problems);
        }

        // A disabled package keeps its tags where they are authored and has none where they are played.
        var published = authored.Where(entry => built.Contains(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, FileName), JsonSerializer.Serialize(published, WriteOptions));
    }

    /// <summary>
    /// Every package the content authors, the disabled ones included (ADR 0015): the switch takes a package out of
    /// the schema, not out of the content, so its tags are still the tags of a package.
    /// </summary>
    private static IEnumerable<string> Authored(string dataDirectory, GameSchema schema)
    {
        var folder = Path.Combine(dataDirectory, GameSchemaBuilder.TiersFolder);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .Select(file => JsonSerializer.Deserialize<TierDto>(File.ReadAllText(file), GameSchemaJson.ReadOptions)?.Id)
            .OfType<string>()
            .Select(id => schema.Aliases.TryGetValue(id, out var current) ? current : id);
    }

    private static IEnumerable<string> Problems(string package, string[] tags, HashSet<string> packages)
    {
        if (!packages.Contains(package))
        {
            yield return $"{SourceFile}: '{package}' is no package this content authors.";
        }

        if (tags.Length is < Fewest or > Most)
        {
            yield return $"{SourceFile}: '{package}' has {tags.Length} tags; a package has {Fewest} or {Most}.";
        }

        foreach (var tag in tags.Where(tag => !Vocabulary.Contains(tag, StringComparer.Ordinal)))
        {
            yield return $"{SourceFile}: '{package}' is tagged '{tag}', which is not in the vocabulary ({string.Join(", ", Vocabulary)}).";
        }

        if (tags.Distinct(StringComparer.Ordinal).Count() != tags.Length)
        {
            yield return $"{SourceFile}: '{package}' names the same tag twice.";
        }
    }
}
