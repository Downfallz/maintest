using System.Globalization;
using System.Text.Json;
using DownfallArena.Application.Work;

namespace DownfallArena.Infrastructure.Work;

/// <summary>
/// The shape a notice count store writes, the same in a directory and in a blob container (N6): one document a UTC
/// day, <c>&lt;yyyy-MM-dd&gt;.json</c>, holding its counts a kind each. A document that does not parse, or a count
/// without its kind, reads as nothing counted, so the next addition writes over it instead of failing on it for good.
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

    public static string Write(IReadOnlyList<NoticeCount> counts) =>
        JsonSerializer.Serialize(new CountsRecord([.. counts.Select(count => new CountRecord(count.Kind, count.Sent, count.Refused, count.Gone))]), Web);

    public static IReadOnlyList<NoticeCount> Read(string json)
    {
        CountsRecord? record;
        try
        {
            record = JsonSerializer.Deserialize<CountsRecord>(json, Web);
        }
        catch (JsonException)
        {
            return [];
        }

        return [.. (record?.Kinds ?? [])
            .Where(count => !string.IsNullOrWhiteSpace(count?.Kind))
            .Select(count => new NoticeCount(count!.Kind!, Math.Max(0, count.Sent), Math.Max(0, count.Refused), Math.Max(0, count.Gone)))];
    }

    private sealed record CountsRecord(IReadOnlyList<CountRecord?>? Kinds);

    private sealed record CountRecord(string? Kind, long Sent, long Refused, long Gone);
}
