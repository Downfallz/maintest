using System.Text.Json;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// Writes what the studio's read-only routes answer to files, so a page served from anywhere can read the
/// content without the engine behind it (ADR 0023). The hosted studio browses these; it fetches an authored
/// document from the repository itself only when it is about to change one.
/// </summary>
internal static class StudioExport
{
    public const string CatalogueFile = "catalogue.json";

    public const string AuditFile = "audit.json";

    public const string WeightsFile = "weights.json";

    public static async Task<int> WriteAsync(StudioApi api, string directory, string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);
        var catalogue = api.Catalogue();
        await WriteAsync(directory, CatalogueFile, Published(catalogue, dataDirectory));
        await WriteAsync(directory, AuditFile, api.AuditReport());
        await WriteAsync(directory, WeightsFile, StudioApi.DefaultWeights());

        Console.WriteLine(catalogue.ContentHash is { } hash
            ? $"Published the content of '{dataDirectory}' to '{directory}' — content {hash[..12]}."
            : $"Published the content of '{dataDirectory}' to '{directory}' — it does not build, and the problems are in {CatalogueFile}.");
        return 0;
    }

    /// <summary>
    /// The catalogue as a published file: the same content, under the directory as it was asked for rather than
    /// as it resolved. What is served from a static host should not carry the absolute path of the machine that
    /// built it, which on a runner is a path nobody can use and nobody should read.
    /// </summary>
    private static ContentCatalogue Published(ContentCatalogue catalogue, string dataDirectory) => new()
    {
        Directory = dataDirectory,
        Creatures = catalogue.Creatures,
        Spells = catalogue.Spells,
        TalentTrees = catalogue.TalentTrees,
        Aliases = catalogue.Aliases,
        ContentHash = catalogue.ContentHash,
        Problems = catalogue.Problems,
        Notes = catalogue.Notes,
    };

    private static async Task WriteAsync(string directory, string name, object payload) =>
        await File.WriteAllTextAsync(Path.Combine(directory, name), JsonSerializer.Serialize(payload, StudioJson.FileOptions));
}
