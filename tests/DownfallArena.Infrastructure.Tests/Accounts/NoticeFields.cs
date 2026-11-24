using System.Text.Json;
using DownfallArena.Application.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>
/// A notice log as a later revision would write it, with fields this one does not know: one on the log and one on an
/// entry (#1211). Marking the entry read writes the log back, and must keep both.
/// </summary>
internal static class NoticeFields
{
    public const string Written = """
        {
          "note": "later",
          "entries": [
            { "id": 1, "kind": "left", "title": "bastien left the table", "body": "b", "url": "/", "tag": "match:t1", "at": "2026-11-22T20:00:00+00:00", "read": false, "icon": "door" }
          ]
        }
        """;

    public static IReadOnlyList<NoticeEntry> Read(IReadOnlyList<NoticeEntry> entries) => [.. entries.Select(entry => entry with { Read = true })];

    public static void ShouldKeepThem(string json)
    {
        var log = JsonDocument.Parse(json).RootElement;
        log.GetProperty("note").GetString().ShouldBe("later");
        var entry = log.GetProperty("entries").EnumerateArray().ShouldHaveSingleItem();
        entry.GetProperty("icon").GetString().ShouldBe("door");
        entry.GetProperty("read").GetBoolean().ShouldBeTrue();
    }
}
