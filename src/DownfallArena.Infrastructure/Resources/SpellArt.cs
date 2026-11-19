using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Application.Catalogue;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources.Schema;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Resources;

/// <summary>
/// How each spell's confirmed cast is drawn in the battle (ADR 0131): a family, a motif and a delivery, keyed by
/// the spell's versioned id, and for a spell that has one its staging, the beats the effects kit plays (ADR 0196). Presentation, not content: kept out of the consolidated schema, so authoring it
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

    /// <summary>The shapes the battle knows how to draw; a motif outside the list would fall to a plain mark.</summary>
    public static readonly IReadOnlyList<string> Motifs =
        ["impact", "slash", "bite", "quill", "wave", "roar", "plates", "spores", "arc", "fire", "ice", "bones", "spirits", "void", "vital", "focus"];

    /// <summary>Whether the caster closes in, sends something, or casts from where it stands.</summary>
    public static readonly IReadOnlyList<string> Deliveries = ["melee", "projectile", "cast"];

    /// <summary>
    /// The beats a staging is made of (ADR 0196): the motif itself, and the primitives of the effects kit. The page
    /// holds the same words in <c>table/battle-staging.js</c>, and its test reads this file against them.
    /// </summary>
    public static readonly IReadOnlyList<string> Beats = ["motif", "burst", "flow", "trail", "gather", "glow", "flash", "shake", "pulse"];

    /// <summary>Where a beat plays.</summary>
    public static readonly IReadOnlyList<string> Anchors = ["caster", "target"];

    /// <summary>The frames of the kit's one texture.</summary>
    public static readonly IReadOnlyList<string> Shapes = ["dot", "spark", "shard", "ring"];

    /// <summary>The colours a beat may take, by name: the family's own, or one of the palette's. Never the marker's gold.</summary>
    public static readonly IReadOnlyList<string> Tints = ["family", "bone", "ember", "blood"];

    /// <summary>How a beat is blended over the field.</summary>
    public static readonly IReadOnlyList<string> Blends = ["normal", "add", "screen"];

    /// <summary>Where a beat heads: all around, along the blow, back against it, or up.</summary>
    public static readonly IReadOnlyList<string> Directions = ["out", "along", "back", "up"];

    /// <summary>The length of the windup at 1×, in milliseconds (<c>table/battle-timing.js</c>): a beat that starts before it ends inside it.</summary>
    public const int WindupMs = 840;

    /// <summary>The length of the windup and the impact together at 1×: no beat starts at or after it, or outlives it.</summary>
    public const int ActionMs = 2540;

    /// <summary>The most beats one staging holds.</summary>
    public const int MaxBeats = 12;

    /// <summary>The most particles one beat holds (docs/art/battle-craft.md, "Burst").</summary>
    public const int MaxParticles = 24;

    /// <summary>The farthest a beat reaches, in hundredths of its figure's radius: 300 clears the body.</summary>
    public const int MaxReach = 400;

    /// <summary>The beats a staging holds once at most: the motif, and the two the flash limits count.</summary>
    private static readonly IReadOnlyList<string> Single = ["motif", "flash", "shake"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

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
            throw new InvalidGameContentException($"{SourceFile}: not a map of spell ids to a family, a motif, a delivery and a staging of beats the kit knows ({exception.Message}).");
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

        foreach (var problem in StagingProblems(spell, visual.Staging))
        {
            yield return problem;
        }
    }

    private static IEnumerable<string> StagingProblems(string spell, List<AuthoredBeat?>? staging)
    {
        if (staging is null)
        {
            yield break;
        }

        // A null among the beats is refused by name before anything reads it: the serializer lets it through.
        if (staging.Any(beat => beat is null))
        {
            yield return $"{SourceFile}: '{spell}' stages a beat that is null.";
            yield break;
        }

        if (staging.Count == 0)
        {
            yield return $"{SourceFile}: '{spell}' stages no beat; leave the staging out to draw the motif.";
        }

        if (staging.Count > MaxBeats)
        {
            yield return $"{SourceFile}: '{spell}' stages {staging.Count} beats, more than {MaxBeats}.";
        }

        foreach (var kind in Single.Where(kind => staging.Count(beat => beat!.Beat == kind) > 1))
        {
            yield return $"{SourceFile}: '{spell}' stages '{kind}' more than once.";
        }

        foreach (var problem in staging.SelectMany(beat => BeatProblems(spell, beat!)))
        {
            yield return problem;
        }
    }

    private static IEnumerable<string> BeatProblems(string spell, AuthoredBeat beat)
    {
        if (beat.At is not { } at)
        {
            yield return $"{SourceFile}: '{spell}' stages a beat '{beat.Beat}' with no 'at': a beat says the millisecond it starts.";
            yield break;
        }

        var where = $"{SourceFile}: '{spell}', the beat '{beat.Beat}' at {Text(at)}";
        if (!Beats.Contains(beat.Beat, StringComparer.Ordinal))
        {
            yield return $"{SourceFile}: '{spell}' stages a beat '{beat.Beat}', which is none of {string.Join(", ", Beats)}.";
        }

        if (at is < 0 or >= ActionMs)
        {
            yield return $"{where} starts at '{Text(at)}', outside the action's 0 to {ActionMs - 1} ms.";
        }
        else if (beat.Beat == "motif" && at < WindupMs)
        {
            yield return $"{where} starts at '{Text(at)}': the motif is drawn in the impact, from {WindupMs} ms.";
        }

        // Compared without adding the two: a huge `lasts` must not wrap around and pass.
        var end = at < WindupMs ? WindupMs : ActionMs;
        if (beat.Lasts is { } lasts && (lasts <= 0 || lasts > end - at))
        {
            yield return $"{where} lasts '{Text(lasts)}' ms: a beat lasts more than 0 and ends inside its window, by {end} ms.";
        }

        (string Name, string? Word, IReadOnlyList<string> Among)[] words =
        [
            ("on", beat.On, Anchors), ("shape", beat.Shape, Shapes), ("tint", beat.Tint, Tints),
            ("blend", beat.Blend, Blends), ("direction", beat.Direction, Directions),
        ];
        foreach (var (name, word, among) in words)
        {
            if (word is not null && !among.Contains(word, StringComparer.Ordinal))
            {
                yield return $"{where} has the {name} '{word}', which is none of {string.Join(", ", among)}.";
            }
        }

        (string Name, int? Value, int Most)[] numbers = [("count", beat.Count, MaxParticles), ("spread", beat.Spread, 360), ("reach", beat.Reach, MaxReach)];
        foreach (var (name, value, most) in numbers)
        {
            if (value is { } number && (number < 1 || number > most))
            {
                yield return $"{where} has a {name} of '{Text(number)}', outside 1 to {most}.";
            }
        }

        if (beat.Strength is { } strength && (strength <= 0 || strength > 1))
        {
            yield return $"{where} has a strength of '{strength.ToString(CultureInfo.InvariantCulture)}', outside 0 to 1.";
        }
    }

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The authored shape: three words, each the empty string when the author left it out, so it is refused by name.
    /// A property it does not know is refused when the file is read: a misspelt <c>staging</c> would otherwise be
    /// dropped, and the spell drawn as its motif with nobody told.
    /// </summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record AuthoredVisual
    {
        public string Family { get; init; } = string.Empty;

        public string Motif { get; init; } = string.Empty;

        public string Delivery { get; init; } = string.Empty;

        public List<AuthoredBeat?>? Staging { get; init; }

        public SpellVisual ToVisual() => new(Family, Motif, Delivery, Staging?.OfType<AuthoredBeat>().Select(beat => beat.ToBeat()).ToList());
    }

    /// <summary>
    /// One authored beat. A property the kit does not know is refused when the file is read, and not dropped: a
    /// misspelt parameter would otherwise play as the default with nobody told.
    /// </summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record AuthoredBeat
    {
        public string Beat { get; init; } = string.Empty;

        public int? At { get; init; }

        public string? On { get; init; }

        public int? Lasts { get; init; }

        public int? Count { get; init; }

        public string? Shape { get; init; }

        public string? Tint { get; init; }

        public string? Blend { get; init; }

        public string? Direction { get; init; }

        public int? Spread { get; init; }

        public int? Reach { get; init; }

        public double? Strength { get; init; }

        public SpellBeat ToBeat() => new(Beat, At ?? 0, On, Lasts, Count, Shape, Tint, Blend, Direction, Spread, Reach, Strength);
    }
}
