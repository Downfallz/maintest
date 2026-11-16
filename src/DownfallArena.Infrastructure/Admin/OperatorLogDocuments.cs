using System.Text.Json;
using DownfallArena.Application.Admin;

namespace DownfallArena.Infrastructure.Admin;

/// <summary>
/// The shape an operator log writes (admin plan, A6), the same in a directory and in a blob container: one document an
/// action, named <c>&lt;id&gt;.json</c>, so names sort as the actions were taken.
/// </summary>
internal static class OperatorLogDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public const string Extension = ".json";

    public static string NameOf(string id) => $"{id}{Extension}";

    /// <summary>The action id a document's name holds, or null for a name no action would have.</summary>
    public static string? IdOf(string name)
    {
        var file = Path.GetFileName(name);
        if (!file.EndsWith(Extension, StringComparison.Ordinal))
        {
            return null;
        }

        var id = file[..^Extension.Length];
        return OperatorAction.IsId(id) ? id : null;
    }

    public static string Write(OperatorAction action) => JsonSerializer.Serialize(action, Web);

    public static OperatorAction Read(string json) =>
        JsonSerializer.Deserialize<OperatorAction>(json, Web) ?? throw new InvalidDataException("An operator action's record is empty.");
}
