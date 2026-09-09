using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources.Schema;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Resources;

/// <summary>
/// Reads authored content from a directory, resolves aliases, validates everything, and produces the consolidated,
/// hashed <see cref="GameSchema"/> (ADR 0009). Also writes and loads that schema.
/// </summary>
public static class GameSchemaBuilder
{
    public const string CreaturesFolder = "Creatures";
    public const string SpellsFolder = "Spells";
    public const string TalentTreesFolder = "TalentTrees";
    public const string AliasesFile = "aliases.json";

    public static GameSchema Build(string dataDirectory) => Build(dataDirectory, notes: null);

    /// <summary>
    /// Builds the schema, adding to <paramref name="notes"/> what the authoring-only <c>enabled</c> switch left
    /// out (ADR 0015). Notes are not problems: the build succeeds with them.
    /// </summary>
    public static GameSchema Build(string dataDirectory, ICollection<string>? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        if (!Directory.Exists(dataDirectory))
        {
            throw new DirectoryNotFoundException($"Content directory '{dataDirectory}' does not exist.");
        }

        var problems = new List<string>();
        var aliases = LoadAliases(Path.Combine(dataDirectory, AliasesFile), problems);
        var creatures = LoadAll<CreatureDefinitionDto>(Path.Combine(dataDirectory, CreaturesFolder), problems);
        var spells = LoadAll<SpellDto>(Path.Combine(dataDirectory, SpellsFolder), problems);
        var trees = LoadAll<TalentTreeDto>(Path.Combine(dataDirectory, TalentTreesFolder), problems);
        ThrowIfAny(problems);

        var resolver = new AliasResolver(aliases);
        var schema = new GameSchema
        {
            Creatures = [.. creatures.Select(creature => Canonical(creature, resolver, problems)).OrderBy(creature => creature.Id, StringComparer.Ordinal)],
            Spells = [.. spells.Select(spell => Canonical(spell, resolver, problems)).OrderBy(spell => spell.Id, StringComparer.Ordinal)],
            TalentTrees = [.. trees.Select(tree => Canonical(tree, resolver, problems)).OrderBy(tree => tree.Id, StringComparer.Ordinal)],
            Aliases = new SortedDictionary<string, string>(aliases, StringComparer.Ordinal),
        };
        ThrowIfAny(problems);

        schema = DisabledContent.Remove(schema, notes, problems);
        ThrowIfAny(problems);

        schema = schema with { ContentHash = ComputeHash(schema) };
        GameSchemaMapper.ToGameResources(schema);
        return schema;
    }

    public static void Write(GameSchema schema, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, GameSchemaJson.SchemaFileName), JsonSerializer.Serialize(schema, GameSchemaJson.WriteOptions));
        File.WriteAllText(Path.Combine(outputDirectory, GameSchemaJson.HashFileName), schema.ContentHash);
    }

    /// <summary>
    /// Loads a consolidated schema, verifies its content hash, and maps it to domain resources.
    /// </summary>
    public static GameResources Load(string schemaPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException($"Game schema '{schemaPath}' does not exist. Run the data builder first.", schemaPath);
        }

        var schema = JsonSerializer.Deserialize<GameSchema>(File.ReadAllText(schemaPath), GameSchemaJson.ReadOptions)
            ?? throw new InvalidGameContentException($"'{schemaPath}' does not contain a game schema.");

        var expected = ComputeHash(schema);
        if (!string.Equals(expected, schema.ContentHash, StringComparison.Ordinal))
        {
            throw new InvalidGameContentException($"'{schemaPath}' has content hash '{schema.ContentHash}' but its content hashes to '{expected}'. Rebuild it with the data builder.");
        }

        return GameSchemaMapper.ToGameResources(schema);
    }

    public static string ComputeHash(GameSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var canonical = JsonSerializer.Serialize(schema with { ContentHash = string.Empty }, GameSchemaJson.HashOptions);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static CreatureDefinitionDto Canonical(CreatureDefinitionDto creature, AliasResolver resolver, List<string> problems)
    {
        var context = $"creature '{creature.Id}'";
        return creature with
        {
            Id = resolver.Resolve<CreatureDefinitionId>(creature.Id, context, problems) ?? creature.Id,
            TalentTreeId = resolver.Resolve<TalentTreeId>(creature.TalentTreeId, context, problems) ?? creature.TalentTreeId,
            StartingSpellIds = [.. creature.StartingSpellIds.Select(spell => resolver.Resolve<SpellId>(spell, context, problems) ?? spell)],
        };
    }

    private static SpellDto Canonical(SpellDto spell, AliasResolver resolver, List<string> problems) =>
        spell with { Id = resolver.Resolve<SpellId>(spell.Id, $"spell '{spell.Id}'", problems) ?? spell.Id };

    private static TalentTreeDto Canonical(TalentTreeDto tree, AliasResolver resolver, List<string> problems)
    {
        var context = $"talent tree '{tree.Id}'";
        return tree with
        {
            Id = resolver.Resolve<TalentTreeId>(tree.Id, context, problems) ?? tree.Id,
            Root = tree.Root is null ? null : Canonical(tree.Root, context, resolver, problems),
        };
    }

    private static TalentNodeDto Canonical(TalentNodeDto node, string context, AliasResolver resolver, List<string> problems)
    {
        var nodeContext = $"{context}, node '{node.Code}'";
        return node with
        {
            Prerequisites = Canonical(node.Prerequisites, nodeContext, resolver, problems),
            Spells = [.. node.Spells.Select(spell => spell with
            {
                Id = resolver.Resolve<SpellId>(spell.Id, nodeContext, problems) ?? spell.Id,
                Prerequisites = Canonical(spell.Prerequisites, nodeContext, resolver, problems),
            })],
            Children = [.. node.Children.Select(child => Canonical(child, context, resolver, problems))],
        };
    }

    private static PrerequisitesDto? Canonical(PrerequisitesDto? prerequisites, string context, AliasResolver resolver, List<string> problems) =>
        prerequisites is null
            ? null
            : prerequisites with
            {
                AllOf = [.. prerequisites.AllOf.Select(spell => resolver.Resolve<SpellId>(spell, context, problems) ?? spell)],
                AnyOf = [.. prerequisites.AnyOf.Select(spell => resolver.Resolve<SpellId>(spell, context, problems) ?? spell)],
            };

    private static Dictionary<string, string> LoadAliases(string path, List<string> problems)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), GameSchemaJson.ReadOptions)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException exception)
        {
            problems.Add($"{Path.GetFileName(path)}: {exception.Message}");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static List<T> LoadAll<T>(string directory, List<string> problems)
        where T : class
    {
        var items = new List<T>();
        if (!Directory.Exists(directory))
        {
            problems.Add($"Content folder '{Path.GetFileName(directory)}' is missing.");
            return items;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            try
            {
                var item = JsonSerializer.Deserialize<T>(File.ReadAllText(file), GameSchemaJson.ReadOptions);
                if (item is null)
                {
                    problems.Add($"{Path.GetRelativePath(directory, file)}: the file is empty.");
                    continue;
                }

                items.Add(item);
            }
            catch (JsonException exception)
            {
                problems.Add($"{Path.GetRelativePath(directory, file)}: {exception.Message}");
            }
        }

        return items;
    }

    private static void ThrowIfAny(List<string> problems)
    {
        if (problems.Count > 0)
        {
            throw new InvalidGameContentException(problems);
        }
    }
}
