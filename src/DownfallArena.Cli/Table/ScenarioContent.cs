using System.Text.Json;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.Infrastructure.Resources;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Where the content a scenario is replayed on comes from (docs/tabletop/scenarios-plan.md, stage S3): the schema
/// the file carries, written to a directory of its own; this host's own schema when it is the content the scenario
/// names; or the content a recording host kept under its hash (ADR 0163). Anything else is refused by name rather
/// than replayed on content the scenario was not cut on.
/// </summary>
internal static class ScenarioContent
{
    /// <summary>The schema file to build a scenario's services over.</summary>
    public static async Task<string> SchemaOfAsync(ScenarioDocument document, CliOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        if (document.Schema is { } carried)
        {
            return await WrittenAsync(document.Content, carried.GetRawText(), options);
        }

        if (string.Equals(OwnVersion(options), document.Content, StringComparison.Ordinal))
        {
            return options.SchemaPath;
        }

        throw new InvalidDataException(
            $"The scenario was cut on content {document.Content}, which it does not carry and which this host's ({OwnVersion(options) ?? "unknown"}) is not. Cut it again with --with-content.");
    }

    /// <summary>
    /// The schema a recorded table was played on: this host's own when it is the one the record names, or the one a
    /// recording host kept beside its runs (<c>content/&lt;hash&gt;/game.schema.json</c> next to the runs' directory).
    /// </summary>
    public static string SchemaOfRecording(string? version, string runs, CliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (version is null || string.Equals(OwnVersion(options), version, StringComparison.Ordinal))
        {
            return options.SchemaPath;
        }

        var kept = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(runs)) ?? runs, ArtifactStores.ContentContainer, version, TableComposer.ContentFile);
        return File.Exists(kept)
            ? kept
            : throw new InvalidDataException($"The session was played on content {version}, which is neither this host's ({OwnVersion(options) ?? "unknown"}) nor kept at {kept}.");
    }

    /// <summary>This host's content hash, read from the file the data builder writes beside the schema.</summary>
    public static string? OwnVersion(CliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var hash = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.SchemaPath)) ?? ".", GameSchemaJson.HashFileName);
        return File.Exists(hash) ? File.ReadAllText(hash).Trim() : null;
    }

    /// <summary>
    /// A carried schema in a directory of its own, beside a copy of this host's presentation, which is kept out of the
    /// content hash and only says how the same ids are drawn (the way a pinned table's content is, ADR 0163).
    /// </summary>
    private static async Task<string> WrittenAsync(string version, string schema, CliOptions options)
    {
        var directory = Path.Combine(Path.GetTempPath(), "downfall-scenario", version, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, TableComposer.ContentFile);
        await File.WriteAllTextAsync(path, schema);
        if (Path.GetDirectoryName(Path.GetFullPath(options.SchemaPath)) is { } beside && Directory.Exists(beside))
        {
            foreach (var presentation in Directory.EnumerateFiles(beside, "*.json").Where(one => !string.Equals(Path.GetFileName(one), TableComposer.ContentFile, StringComparison.Ordinal)))
            {
                File.Copy(presentation, Path.Combine(directory, Path.GetFileName(presentation)), overwrite: true);
            }
        }

        return path;
    }

    /// <summary>A schema file's content, to carry in a scenario cut with <c>--with-content</c>.</summary>
    public static async Task<JsonElement> CarriedAsync(string schemaPath)
    {
        using var schema = JsonDocument.Parse(await File.ReadAllTextAsync(schemaPath));
        return schema.RootElement.Clone();
    }
}
