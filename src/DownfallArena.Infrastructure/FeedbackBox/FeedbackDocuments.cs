using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Application.FeedbackBox;

namespace DownfallArena.Infrastructure.FeedbackBox;

/// <summary>
/// The shape a feedback store writes (ADR 0189), the same in a directory and in a blob container: one document a piece
/// of feedback, named <c>&lt;id&gt;.json</c>, so names sort as feedback came.
/// </summary>
internal static class FeedbackDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public const string Extension = ".json";

    public static string NameOf(string id) => $"{id}{Extension}";

    /// <summary>The feedback id a document's name holds, or null for a name no feedback would have.</summary>
    public static string? IdOf(string name)
    {
        var file = Path.GetFileName(name);
        if (!file.EndsWith(Extension, StringComparison.Ordinal))
        {
            return null;
        }

        var id = file[..^Extension.Length];
        return Feedback.IsId(id) ? id : null;
    }

    public static string Write(Feedback feedback) => JsonSerializer.Serialize(feedback, Web);

    public static Feedback Read(string json) =>
        JsonSerializer.Deserialize<Feedback>(json, Web) ?? throw new InvalidDataException("A feedback record is empty.");
}
