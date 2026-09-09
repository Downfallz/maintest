using System.Text.Json;
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

    private readonly string _root;

    public ContentStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _root = Path.GetFullPath(dataDirectory);
    }

    /// <summary>The content directory this store reads and writes, as an absolute path.</summary>
    public string Root => _root;

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
        if (!Directory.Exists(_root))
        {
            throw new DirectoryNotFoundException($"Content directory '{_root}' does not exist.");
        }

        var notes = new List<string>();
        string? contentHash = null;
        IReadOnlyList<string> problems = [];
        try
        {
            contentHash = GameSchemaBuilder.Build(_root, notes).ContentHash;
        }
        catch (InvalidGameContentException exception)
        {
            problems = exception.Problems;
        }

        return new ContentCatalogue
        {
            Directory = _root,
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
        var schema = GameSchemaBuilder.Build(_root, notes);
        GameSchemaBuilder.Write(schema, outputDirectory);
        return (schema.ContentHash, notes);
    }

    /// <summary>
    /// Writes one document to its file, creating the folders it needs. The JSON must parse into the DTO of its
    /// kind with no unknown members; what lands on disk is that same JSON, indented.
    /// </summary>
    public string Save(ContentKind kind, string relativePath, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var path = Resolve(kind, relativePath);

        using var document = ParseOrThrow(json, relativePath);
        ValidateAgainstDto(kind, json, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteAtomically(path, JsonSerializer.Serialize(document.RootElement, IndentedOptions));
        return Relative(path);
    }

    public void Delete(ContentKind kind, string relativePath)
    {
        var path = Resolve(kind, relativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"'{relativePath}' does not exist under '{_root}'.", relativePath);
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
        WriteAtomically(Path.Combine(_root, GameSchemaBuilder.AliasesFile), JsonSerializer.Serialize(sorted, IndentedOptions));
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
        var path = Path.GetFullPath(Path.Combine(_root, relativePath));
        var expectedPrefix = Path.GetFullPath(Path.Combine(_root, folder)) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            throw new InvalidGameContentException($"'{relativePath}' is not under '{folder}/', where {kind} content lives.");
        }

        return path;
    }

    private string Relative(string fullPath) => Path.GetRelativePath(_root, fullPath).Replace(Path.DirectorySeparatorChar, '/');

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

            if (parsed is null)
            {
                throw new InvalidGameContentException($"{relativePath}: the document is empty.");
            }
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
    private static void WriteAtomically(string path, string content)
    {
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
        var path = Path.Combine(_root, GameSchemaBuilder.AliasesFile);
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
        var folder = Path.Combine(_root, FolderOf(kind));
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
