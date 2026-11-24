using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Application.Work;

namespace DownfallArena.Infrastructure.Work;

/// <summary>
/// The shape a notice count store writes, the same in a directory and in a blob container (N6): one document a UTC
/// day, <c>&lt;yyyy-MM-dd&gt;.json</c>, holding its counts a kind each. A document that does not parse, or a count
/// without its kind, reads as nothing counted, so the next addition writes over it instead of failing on it for good.
/// A field this revision does not know, on the day or on a kind, is written back as it was read: during a deploy the
/// revision before runs beside the next, and would otherwise wipe a count the next one added (#1151). A kind whose
/// only counts are ones this revision does not know is not read: it is kept, and the admin panel leaves it out.
/// </summary>
internal static class NoticeCountDocuments
{
    private const string Extension = ".json";
    private const string Format = "yyyy-MM-dd";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string NameOf(DateOnly day) => day.ToString(Format, CultureInfo.InvariantCulture) + Extension;

    /// <summary>The day a document's name holds, or none for a name no day's document would have.</summary>
    public static DateOnly? DayOf(string name)
    {
        var file = Path.GetFileName(name);
        return file.EndsWith(Extension, StringComparison.Ordinal)
            && DateOnly.TryParseExact(file[..^Extension.Length], Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;
    }

    /// <summary>
    /// The day's counts as a document, keeping what <paramref name="before" />, the document they were read from, holds
    /// that this revision does not know: the fields of the day, those of each kind, and a kind that counts only them.
    /// </summary>
    public static string Write(IReadOnlyList<NoticeCount> counts, string? before = null)
    {
        ArgumentNullException.ThrowIfNull(counts);
        var kept = Parse(before);
        var unknown = (kept?.Kinds ?? [])
            .Where(kind => !string.IsNullOrWhiteSpace(kind?.Kind) && kind.Unknown is { Count: > 0 })
            .GroupBy(kind => kind!.Kind!, StringComparer.Ordinal)
            .ToDictionary(kind => kind.Key, kind => kind.First()!.Unknown, StringComparer.Ordinal);
        var written = counts.Select(count => new CountRecord(count.Kind, count.Sent, count.Refused, count.Gone) { Unknown = unknown.GetValueOrDefault(count.Kind) }).ToList();
        written.AddRange(unknown.Where(kind => !counts.Any(count => string.Equals(count.Kind, kind.Key, StringComparison.Ordinal)))
            .Select(kind => new CountRecord(kind.Key, 0, 0, 0) { Unknown = kind.Value }));
        return JsonSerializer.Serialize(new CountsRecord([.. written.OrderBy(count => count.Kind, StringComparer.Ordinal)]) { Unknown = kept?.Unknown }, Web);
    }

    public static IReadOnlyList<NoticeCount> Read(string json)
    {
        return [.. (Parse(json)?.Kinds ?? [])
            .Where(count => !string.IsNullOrWhiteSpace(count?.Kind))
            .Select(count => new NoticeCount(count!.Kind!, Math.Max(0, count.Sent), Math.Max(0, count.Refused), Math.Max(0, count.Gone)))
            // A kind whose only counts are ones this revision does not know is kept on the next write, not read as zeros.
            .Where(count => !count.IsEmpty)];
    }

    private static CountsRecord? Parse(string? json)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CountsRecord>(json, Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record CountsRecord(IReadOnlyList<CountRecord?>? Kinds)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; init; }
    }

    private sealed record CountRecord(string? Kind, long Sent, long Refused, long Gone)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; init; }
    }
}
