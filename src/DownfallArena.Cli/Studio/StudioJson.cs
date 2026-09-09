using System.Text.Json;
using System.Text.Json.Serialization;

namespace DownfallArena.Cli.Studio;

/// <summary>
/// The studio's JSON conventions, shared by what the API answers and what a run writes beside its artifacts:
/// camelCase like every other artifact of this repository, enums by name.
/// </summary>
internal static class StudioJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The same conventions, indented, for what lands in a file an author may open.</summary>
    public static JsonSerializerOptions FileOptions { get; } = new(Options) { WriteIndented = true };
}
