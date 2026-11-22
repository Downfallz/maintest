using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The shape a notice store writes, the same in a directory and in a blob container (ADR 0201): one document an
/// account at <c>notices/&lt;accountId&gt;.json</c>, holding its entries. A kind is written by its name; an entry
/// of a kind this host does not know, written by a later one, is skipped rather than refusing the whole log, and
/// goes at this host's next write: a new kind is sent only once every process reads it (ADR 0201).
/// </summary>
internal static class NoticeDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string PathOf(AccountId account) => $"notices/{account.Value}.json";

    public static string Write(IReadOnlyList<NoticeEntry> entries) =>
        JsonSerializer.Serialize(new NoticesRecord([.. entries.Select(entry => new EntryRecord(entry.Id, NoticeKinds.Name(entry.Kind), entry.Title, entry.Body, entry.Url, entry.Tag, entry.At, entry.Read))]), Web);

    /// <summary>
    /// The entries a document holds. One that does not parse reads as none, at the version it was read at, so the next
    /// notice writes over it instead of every later notice failing on it for good.
    /// </summary>
    public static IReadOnlyList<NoticeEntry> Read(string json)
    {
        NoticesRecord? record;
        try
        {
            record = JsonSerializer.Deserialize<NoticesRecord>(json, Web);
        }
        catch (JsonException)
        {
            return [];
        }

        return record is null ? [] : [.. (record.Entries ?? [])
            .Where(entry => entry is { Title: not null, Body: not null, Url: not null, Tag: not null } && NoticeKinds.Parse(entry.Kind) is not null)
            .Select(entry => new NoticeEntry(entry!.Id, NoticeKinds.Parse(entry.Kind)!.Value, entry.Title!, entry.Body!, entry.Url!, entry.Tag!, entry.At, entry.Seen))];
    }

    private sealed record NoticesRecord(IReadOnlyList<EntryRecord?>? Entries);

    private sealed record EntryRecord(long Id, string? Kind, string? Title, string? Body, string? Url, string? Tag, DateTimeOffset At, [property: JsonPropertyName("read")] bool Seen);
}
