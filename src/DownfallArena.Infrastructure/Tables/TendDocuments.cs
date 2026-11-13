using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Application.Tables.Tending;

namespace DownfallArena.Infrastructure.Tables;

/// <summary>
/// The shape a tend log writes, the same in a directory and in a blob container: one document a run, named
/// <c>&lt;id&gt;.json</c>, so names sort as the runs started.
/// </summary>
internal static class TendDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public const string Extension = ".json";

    public static string NameOf(string id) => $"{id}{Extension}";

    /// <summary>The run id a document's name holds, or null for a name no run would have.</summary>
    public static string? IdOf(string name)
    {
        var file = Path.GetFileName(name);
        if (!file.EndsWith(Extension, StringComparison.Ordinal))
        {
            return null;
        }

        var id = file[..^Extension.Length];
        return TendRun.IsId(id) ? id : null;
    }

    public static string Write(TendRun run) => JsonSerializer.Serialize(run, Web);

    public static TendRun Read(string json) =>
        JsonSerializer.Deserialize<TendRun>(json, Web) ?? throw new InvalidDataException("A tending run's record is empty.");
}
