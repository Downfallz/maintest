using System.Text.Json;
using DownfallArena.Application.Content;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources.Schema;

namespace DownfallArena.Infrastructure.Resources.Authoring;

/// <summary>
/// Reads and writes the authored content files of one content directory for the studio (ADR 0015). Reading never
/// throws on a bad file: it reports it. Writing validates the document against its DTO first, so a typo in a
/// field name is refused here rather than at the next build, and never leaves the content directory.
/// </summary>
public sealed class ContentStore
{
    private const string TemporarySuffix = ".tmp";

    private static readonly JsonSerializerOptions IndentedOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Stands in for the document of a file that does not parse, so the studio can still list it.</summary>
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    public ContentStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        Root = Path.GetFullPath(dataDirectory);
    }

    /// <summary>The content directory this store reads and writes, as an absolute path.</summary>
    public string Root { get; }

    public static string FolderOf(ContentKind kind) => kind switch
    {
        ContentKind.Creature => GameSchemaBuilder.CreaturesFolder,
        ContentKind.Spell => GameSchemaBuilder.SpellsFolder,
        ContentKind.TalentTree => GameSchemaBuilder.TalentTreesFolder,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// Every authored file, plus the outcome of building them: the content hash, or the problems that stopped it.
    /// </summary>
    public ContentCatalogue Read()
    {
        if (!Directory.Exists(Root))
        {
            throw new DirectoryNotFoundException($"Content directory '{Root}' does not exist.");
        }

        var notes = new List<string>();
        string? contentHash = null;
        IReadOnlyList<string> problems = [];
        try
        {
            contentHash = GameSchemaBuilder.Build(Root, notes).ContentHash;
        }
        catch (InvalidGameContentException exception)
        {
            problems = exception.Problems;
        }

        return new ContentCatalogue
        {
            Directory = Root,
            Creatures = ReadAll<CreatureDefinitionDto>(ContentKind.Creature),
            Spells = ReadAll<SpellDto>(ContentKind.Spell),
            TalentTrees = ReadAll<TalentTreeDto>(ContentKind.TalentTree),
            Aliases = ReadAliases(),
            ContentHash = contentHash,
            Problems = problems,
            Notes = notes,
        };
    }

    /// <summary>
    /// Builds the consolidated schema and writes it to <paramref name="outputDirectory"/>, returning the content
    /// hash and the notes. Problems come back as <see cref="InvalidGameContentException"/>.
    /// </summary>
    public (string ContentHash, IReadOnlyList<string> Notes) Build(string outputDirectory)
    {
        var notes = new List<string>();
        var schema = GameSchemaBuilder.Build(Root, notes);
        GameSchemaBuilder.Write(schema, outputDirectory);
        return (schema.ContentHash, notes);
    }

    /// <summary>
    /// What the audit makes of the content as it is authored right now: content that no creature can reach,
    /// open or cast (<see cref="ContentAudit"/>). The schema is built in memory rather than read from disk, so
    /// the findings are about what the author is looking at and not about the last build.
    /// </summary>
    public ContentAuditReport Audit(RuleSet rules) =>
        ContentAudit.Of(GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(Root, notes: null)), rules);

    /// <summary>
    /// Writes one document to its file, creating the folders it needs. The JSON must parse into the DTO of its
    /// kind with no unknown members; what lands on disk is that same JSON, indented.
    /// <para>
    /// With <paramref name="overwrite"/> false the write is refused when the file is already there. Cutting a new
    /// version and creating an item both name a file the author believes is new, and silently replacing the one
    /// that is there would lose content the alias then stops pointing at.
    /// </para>
    /// </summary>
    public string Save(ContentKind kind, string relativePath, string json, bool overwrite = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var path = Resolve(kind, relativePath);

        using var document = ParseOrThrow(json, relativePath);
        ValidateAgainstDto(kind, json, relativePath);

        if (!overwrite && File.Exists(path))
        {
            throw new InvalidGameContentException($"'{relativePath}' already exists. Open it to change it, or pick another name.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteAtomically(path, JsonSerializer.Serialize(document.RootElement, IndentedOptions));
        return Relative(path);
    }

    public void Delete(ContentKind kind, string relativePath)
    {
        var path = Resolve(kind, relativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"'{relativePath}' does not exist under '{Root}'.", relativePath);
        }

        File.Delete(path);
    }

    /// <summary>Rewrites <c>aliases.json</c>, sorted, so the file stays reviewable in a diff.</summary>
    public void SaveAliases(IReadOnlyDictionary<string, string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        foreach (var (alias, target) in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(target))
            {
                throw new InvalidGameContentException("An alias and its target cannot be empty.");
            }
        }

        var sorted = new SortedDictionary<string, string>(aliases.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), StringComparer.Ordinal);
        WriteAtomically(Path.Combine(Root, GameSchemaBuilder.AliasesFile), JsonSerializer.Serialize(sorted, IndentedOptions));
    }

    /// <summary>
    /// The absolute path of <paramref name="relativePath"/>, refusing anything that is not a JSON file inside the
    /// folder of its kind. The studio takes this path from a browser, so containment is checked, not assumed.
    /// </summary>
    private string Resolve(ContentKind kind, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath) || relativePath.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidGameContentException($"'{relativePath}' is not a path inside the content directory.");
        }

        if (!relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidGameContentException($"'{relativePath}' is not a '.json' file.");
        }

        var folder = FolderOf(kind);
        var path = Path.GetFullPath(Path.Combine(Root, relativePath));
        var expectedPrefix = Path.GetFullPath(Path.Combine(Root, folder)) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new InvalidGameContentException($"'{relativePath}' is not under '{folder}/', where {kind} content lives.");
        }

        return path;
    }

    private string Relative(string fullPath) => Path.GetRelativePath(Root, fullPath).Replace(Path.DirectorySeparatorChar, '/');

    private static JsonDocument ParseOrThrow(string json, string relativePath)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidGameContentException($"{relativePath}: {exception.Message}");
        }
    }

    private static void ValidateAgainstDto(ContentKind kind, string json, string relativePath)
    {
        try
        {
            var parsed = kind switch
            {
                ContentKind.Creature => (object?)JsonSerializer.Deserialize<CreatureDefinitionDto>(json, GameSchemaJson.ReadOptions),
                ContentKind.Spell => JsonSerializer.Deserialize<SpellDto>(json, GameSchemaJson.ReadOptions),
                ContentKind.TalentTree => JsonSerializer.Deserialize<TalentTreeDto>(json, GameSchemaJson.ReadOptions),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

            _ = parsed ?? throw new InvalidGameContentException($"{relativePath}: the document is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidGameContentException($"{relativePath}: {exception.Message}");
        }
    }

    /// <summary>
    /// Writes through a temporary file so a failure leaves the previous content intact rather than half a
    /// document, and cleans the temporary up so nothing unreviewable is left in the content tree.
    /// </summary>
    /// <summary>
    /// Writes through a temporary file so a crash cannot leave a half-written document, and ends the file with a
    /// newline: every authored file in the repository has one, and a save that dropped it made a diff out of a
    /// line nobody touched. <see cref="JsonSerializer"/> never writes one, so it is added here rather than at
    /// each call.
    /// </summary>
    private static void WriteAtomically(string path, string content)
    {
        content = content.EndsWith('\n') ? content : content + '\n';

        var temporary = path + TemporarySuffix;
        try
        {
            File.WriteAllText(temporary, content);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private Dictionary<string, string> ReadAliases()
    {
        var path = Path.Combine(Root, GameSchemaBuilder.AliasesFile);
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), GameSchemaJson.ReadOptions)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private List<ContentDocument> ReadAll<TDto>(ContentKind kind)
        where TDto : class
    {
        var folder = Path.Combine(Root, FolderOf(kind));
        var documents = new List<ContentDocument>();
        if (!Directory.Exists(folder))
        {
            return documents;
        }

        foreach (var file in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var text = File.ReadAllText(file);
            var relative = Relative(file);
            JsonElement element;
            try
            {
                using var parsed = JsonDocument.Parse(text);
                element = parsed.RootElement.Clone();
            }
            catch (JsonException exception)
            {
                documents.Add(Broken(kind, relative, exception.Message));
                continue;
            }

            string? problem = null;
            try
            {
                _ = JsonSerializer.Deserialize<TDto>(text, GameSchemaJson.ReadOptions);
            }
            catch (JsonException exception)
            {
                problem = exception.Message;
            }

            documents.Add(new ContentDocument
            {
                Kind = kind,
                Path = relative,
                Id = Text(element, "id") ?? relative,
                Name = Text(element, "name") ?? string.Empty,
                Enabled = !(element.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.False),
                Document = element,
                Problem = problem,
            });
        }

        return documents;
    }

    private static ContentDocument Broken(ContentKind kind, string relativePath, string problem) =>
        new()
        {
            Kind = kind,
            Path = relativePath,
            Id = relativePath,
            Name = string.Empty,
            Enabled = true,
            Document = EmptyObject,
            Problem = problem,
        };

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
