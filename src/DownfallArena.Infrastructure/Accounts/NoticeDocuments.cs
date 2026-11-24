using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The shape a notice store writes, the same in a directory and in a blob container (ADR 0201): one document an
/// account at <c>notices/&lt;accountId&gt;.json</c>, holding its entries. A kind is written by its name; an entry
/// of a kind this host does not know, written by a later one, is skipped rather than refusing the whole log, and
/// goes at this host's next write: a new kind is sent only once every process reads it (ADR 0201). A field this
/// revision does not know, on the log or on an entry of a kind it reads, is written back as it was read: during a deploy
/// the revision before runs beside the next, and would otherwise wipe what the next one added (#1211).
/// </summary>
internal static class NoticeDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Where every log is, ahead of its account's id.</summary>
    public const string Prefix = "notices/";

    private const string Extension = ".json";

    public static string PathOf(AccountId account) => $"{Prefix}{account.Value}{Extension}";

    /// <summary>The account a log's name is the log of, or none for a name no log has.</summary>
    public static AccountId? AccountOf(string name) =>
        name.StartsWith(Prefix, StringComparison.Ordinal) && name.EndsWith(Extension, StringComparison.Ordinal) && AccountId.TryParse(name[Prefix.Length..^Extension.Length], out var account)
            ? account
            : null;

    /// <summary>
    /// The entries as a document, keeping what <paramref name="before" />, the document they were read from, holds that
    /// this revision does not know: the fields of the log, and those of each entry of a kind it reads, matched by its id.
    /// </summary>
    public static string Write(IReadOnlyList<NoticeEntry> entries, string? before = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var kept = Parse(before);
        var unknown = (kept?.Entries ?? [])
            // Only an entry this revision reads: one of a kind it skips goes at this write, and its id can be one this
            // revision hands out again, which must not inherit its fields.
            .Where(entry => entry?.Unknown is { Count: > 0 } && NoticeKinds.Parse(entry.Kind) is not null)
            .GroupBy(entry => entry!.Id)
            .ToDictionary(entry => entry.Key, entry => entry.First()!.Unknown);
        return JsonSerializer.Serialize(new NoticesRecord([.. entries.Select(entry =>
            new EntryRecord(entry.Id, NoticeKinds.Name(entry.Kind), entry.Title, entry.Body, entry.Url, entry.Tag, entry.At, entry.Read) { Unknown = unknown.GetValueOrDefault(entry.Id) })])
        { Unknown = kept?.Unknown }, Web);
    }

    /// <summary>
    /// The entries a document holds. One that does not parse reads as none, at the version it was read at, so the next
    /// notice writes over it instead of every later notice failing on it for good.
    /// </summary>
    public static IReadOnlyList<NoticeEntry> Read(string json)
    {
        var record = Parse(json);
        return record is null ? [] : [.. (record.Entries ?? [])
            .Where(entry => entry is { Title: not null, Body: not null, Url: not null, Tag: not null } && NoticeKinds.Parse(entry.Kind) is not null)
            .Select(entry => new NoticeEntry(entry!.Id, NoticeKinds.Parse(entry.Kind)!.Value, entry.Title!, entry.Body!, entry.Url!, entry.Tag!, entry.At, entry.Seen))];
    }

    private static NoticesRecord? Parse(string? json)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<NoticesRecord>(json, Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record NoticesRecord(IReadOnlyList<EntryRecord?>? Entries)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; init; }
    }

    private sealed record EntryRecord(long Id, string? Kind, string? Title, string? Body, string? Url, string? Tag, DateTimeOffset At, [property: JsonPropertyName("read")] bool Seen)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; init; }
    }
}
