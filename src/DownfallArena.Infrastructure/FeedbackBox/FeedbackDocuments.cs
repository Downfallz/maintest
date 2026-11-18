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

    /// <summary>
    /// The start every name of feedback sent on each day of the span shares, its first day first: ids start with the
    /// day, <c>yyyyMMdd</c>, so a store lists those days alone. None when the span ends before it begins.
    /// </summary>
    public static IEnumerable<string> DaysOf(DateTimeOffset after, DateTimeOffset until)
    {
        for (var day = after.UtcDateTime.Date; day <= until.UtcDateTime.Date; day = day.AddDays(1))
        {
            yield return day.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Whether the feedback the id names was sent within the span: after its start, and no later than its end.</summary>
    public static bool SentWithin(string id, DateTimeOffset after, DateTimeOffset until) =>
        Feedback.SentAt(id) is { } at && at > after && at <= until;

    /// <summary>
    /// The index of feedback's own document (#866), beside the records: its name is no feedback id, so no listing of
    /// ids holds it.
    /// </summary>
    public const string IndexName = "index.json";

    public static string Write(Feedback feedback) => JsonSerializer.Serialize(feedback, Web);

    public static string WriteIndex(FeedbackIndexContent content) => JsonSerializer.Serialize(content, Compact);

    /// <summary>The index as kept, or none for one that cannot be read: the next read builds it again.</summary>
    public static FeedbackIndexContent? ReadIndex(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<FeedbackIndexContent>(json, Compact) is { Rows: not null, Closed: not null } content ? content : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The index is rewritten with each change: written on one line, its nulls left out.</summary>
    private static readonly JsonSerializerOptions Compact = new(Web) { WriteIndented = false };

    public static Feedback Read(string json) =>
        JsonSerializer.Deserialize<Feedback>(json, Web) ?? throw new InvalidDataException("A feedback record is empty.");
}
