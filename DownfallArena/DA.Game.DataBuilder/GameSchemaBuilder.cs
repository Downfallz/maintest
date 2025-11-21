using DA.Game.Shared.Contracts.Resources.Json;
using DA.Game.Shared.Utilities;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DA.Game.DataBuilder;

internal static class GameSchemaBuilder
{
    /// <summary>
    /// Builds the consolidated game schema from the Data folder.
    /// </summary>
    /// <param name="logger">
    ///   Optional logger for diagnostics. Can be null for silent execution.
    /// </param>
    /// <param name="baseDirectory">
    ///   Optional override for testing or CLI. Defaults to AppContext.BaseDirectory.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional cancellation token for async operations.
    /// </param>
    public static async Task BuildAsync(
        ILogger? logger = null,
        string? baseDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var baseDir = baseDirectory ?? AppContext.BaseDirectory;
        var (sourceDir, dstDir) = PrepareDirectories(baseDir, logger);

        logger?.LogDebug("Source directory: {SourceDir}", sourceDir);
        logger?.LogDebug("Destination directory: {DestinationDir}", dstDir);

        var spellsDir = Path.Combine(sourceDir, DownfallArenaJsonOptions.FOLDER_SPELLS);
        var creaturesDir = Path.Combine(sourceDir, DownfallArenaJsonOptions.FOLDER_CREATURES);

        var spells = await LoadDefinitionsAsync<SpellDefinitionDto>(spellsDir, logger, cancellationToken);
        var creatures = await LoadDefinitionsAsync<CreatureDefinitionDto>(creaturesDir, logger, cancellationToken);
        var aliases = await ReadAliasesAsync(sourceDir, logger, cancellationToken);

        logger?.LogInformation("Loaded {SpellCount} spells and {CreatureCount} creatures.",
            spells.Count, creatures.Count);

        var schemaWithoutHash = CreateSchema(spells, creatures, aliases);

        ValidateSchema(schemaWithoutHash, logger);

        var schemaWithHash = AttachHash(schemaWithoutHash, logger);

        await SaveSchemaAsync(dstDir, schemaWithHash, logger, cancellationToken);

        logger?.LogInformation("Game schema build completed successfully. Hash: {Hash}", schemaWithHash.BuildHash);
    }

    /// <summary>
    /// Prepares the source and destination directories.
    /// </summary>
    private static (string sourceDir, string dstDir) PrepareDirectories(string baseDir, ILogger? logger)
    {
        var sourceDir = Path.Combine(baseDir, DownfallArenaJsonOptions.FOLDER_DATA);
        var dstDir = Path.Combine(sourceDir, DownfallArenaJsonOptions.FOLDER_DST);

        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException(
                $"Data directory not found: '{sourceDir}'. " +
                "Make sure the 'Data' folder is copied to the output directory.");
        }

        Directory.CreateDirectory(dstDir);
        logger?.LogDebug("Ensured destination directory exists: {DstDir}", dstDir);

        return (sourceDir, dstDir);
    }

    /// <summary>
    /// Creates the initial schema object before attaching the hash.
    /// </summary>
    private static GameSchema CreateSchema(
        IReadOnlyList<SpellDefinitionDto> spells,
        IReadOnlyList<CreatureDefinitionDto> creatures,
        Dictionary<string, string>? aliases)
        => new()
        {
            SchemaVersion = 1,
            Spells = spells,
            Creatures = creatures,
            Aliases = aliases,
            BuildHash = string.Empty // temporarily empty, replaced later
        };

    /// <summary>
    /// Basic validation hook for the in-memory schema before hashing and saving.
    /// This is the place to enforce invariants (e.g. non-empty collections, unique IDs, etc.).
    /// </summary>
    private static void ValidateSchema(GameSchema schema, ILogger? logger)
    {
        // Example of simple guards; adjust to your domain rules
        if (schema.Spells is null || schema.Spells.Count == 0)
            throw new InvalidOperationException("Game schema must contain at least one spell.");

        if (schema.Creatures is null || schema.Creatures.Count == 0)
            throw new InvalidOperationException("Game schema must contain at least one creature.");

        // Example: warn if aliases is missing (but not fatal)
        if (schema.Aliases is null)
        {
            logger?.LogWarning("No alias file found. Aliases will be null in the generated schema.");
        }

        // TODO: If you want full JSON Schema validation, this is where you would:
        // - Load a JSON Schema (e.g. game.schema.definition.json) from disk.
        // - Use a library such as JsonSchema.Net or NJsonSchema to validate `schema`.
        // - Throw an exception if validation fails.
    }

    /// <summary>
    /// Computes the schema hash (using a stable, minified JSON representation)
    /// and returns a new immutable instance with the hash injected.
    /// </summary>
    private static GameSchema AttachHash(GameSchema schema, ILogger? logger)
    {
        var jsonForHash = JsonSerializer.Serialize(schema, DownfallArenaJsonOptions.HashOptions);
        var hash = ComputeHash(jsonForHash);

        logger?.LogDebug("Computed schema hash: {Hash}", hash);

        return schema with { BuildHash = hash };
    }

    /// <summary>
    /// Computes a SHA256 hash of the provided string.
    /// </summary>
    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Writes the final schema file and hash file to the destination directory.
    /// </summary>
    private static async Task SaveSchemaAsync(
        string dstDir,
        GameSchema schema,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(schema, DownfallArenaJsonOptions.WriteOptions);
        var schemaPath = Path.Combine(dstDir, DownfallArenaJsonOptions.FILENAME_GAMESCHEMA);
        var hashPath = Path.Combine(dstDir, DownfallArenaJsonOptions.FILENAME_SCHEMAHASH);

        await File.WriteAllTextAsync(schemaPath, json, cancellationToken);
        await File.WriteAllTextAsync(hashPath, schema.BuildHash, cancellationToken);

        logger?.LogInformation("Schema written to {SchemaPath}", schemaPath);
        logger?.LogInformation("Hash written to {HashPath}", hashPath);
    }

    /// <summary>
    /// Reads optional alias mappings from aliases.json.
    /// </summary>
    private static async Task<Dictionary<string, string>?> ReadAliasesAsync(
        string sourceDir,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var aliasPath = Path.Combine(sourceDir, DownfallArenaJsonOptions.FILENAME_ALIASES);
        if (!File.Exists(aliasPath))
        {
            logger?.LogDebug("Alias file not found at {AliasPath}.", aliasPath);
            return null;
        }

        logger?.LogDebug("Reading aliases from {AliasPath}", aliasPath);

        var json = await File.ReadAllTextAsync(aliasPath, cancellationToken);
        var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(json, DownfallArenaJsonOptions.ReadOptions);

        if (aliases is null)
            throw new InvalidDataException($"Failed to deserialize aliases from '{aliasPath}'.");

        return aliases;
    }

    /// <summary>
    /// Loads all JSON files in a directory into strongly-typed DTOs.
    /// </summary>
    private static async Task<IReadOnlyList<T>> LoadDefinitionsAsync<T>(
        string dir,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(dir))
        {
            logger?.LogWarning("Directory '{Dir}' does not exist. No {TypeName} will be loaded.",
                dir, typeof(T).Name);
            return Array.Empty<T>();
        }

        var result = new List<T>();
        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).ToList();

        logger?.LogDebug("Found {FileCount} JSON files in {Dir} for type {TypeName}.",
            files.Count, dir, typeof(T).Name);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            logger?.LogDebug("Reading {File}", file);

            var fileJson = await File.ReadAllTextAsync(file, cancellationToken);
            var obj = JsonSerializer.Deserialize<T>(fileJson, DownfallArenaJsonOptions.ReadOptions);

            if (obj is null)
                throw new InvalidDataException($"Failed to deserialize '{file}' as {typeof(T).Name}.");

            result.Add(obj);
        }

        return result;
    }
}
