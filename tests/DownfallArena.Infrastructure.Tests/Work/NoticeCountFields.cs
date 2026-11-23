using System.Text.Json;

namespace DownfallArena.Infrastructure.Tests.Work;

/// <summary>
/// A day of counts as a later revision would write it, with fields this one does not know: one on the day, one on a
/// kind this revision counts, and a kind that only counts what this revision does not (#1151).
/// </summary>
internal static class NoticeCountFields
{
    public static readonly DateOnly Day = new(2026, 11, 23);

    public const string Written = """
        {
          "note": "later",
          "kinds": [
            { "kind": "invitation", "dropped": 4 },
            { "kind": "turn", "sent": 2, "gone": 1, "dropped": 3 }
          ]
        }
        """;

    public static void ShouldKeepThem(string json)
    {
        var day = JsonDocument.Parse(json).RootElement;
        day.GetProperty("note").GetString().ShouldBe("later");
        var kinds = day.GetProperty("kinds").EnumerateArray().ToDictionary(kind => kind.GetProperty("kind").GetString()!);
        kinds["turn"].GetProperty("dropped").GetInt32().ShouldBe(3);
        kinds["turn"].GetProperty("sent").GetInt32().ShouldBe(3);
        kinds["invitation"].GetProperty("dropped").GetInt32().ShouldBe(4);
    }
}
