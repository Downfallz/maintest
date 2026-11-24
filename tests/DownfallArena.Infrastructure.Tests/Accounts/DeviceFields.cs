using System.Text.Json;
using DownfallArena.Application.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>
/// A device list as a later revision would write it, with fields this one does not know: one on the list and one on a
/// device (#1211). Adding a second device writes the list back, and must keep both.
/// </summary>
internal static class DeviceFields
{
    public const string Written = """
        {
          "note": "later",
          "devices": [
            { "endpoint": "https://fcm.googleapis.com/fcm/send/phone", "p256dh": "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A", "auth": "AAECAwQFBgcICQoLDA0ODw", "addedAt": "2026-11-22T20:00:00+00:00", "label": "Pixel" }
          ]
        }
        """;

    public static IReadOnlyList<PushDevice> Added(IReadOnlyList<PushDevice> devices) =>
        [.. devices, new PushDevice("https://fcm.googleapis.com/fcm/send/laptop", "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A", "AAECAwQFBgcICQoLDA0ODw", DateTimeOffset.UnixEpoch)];

    public static void ShouldKeepThem(string json)
    {
        var list = JsonDocument.Parse(json).RootElement;
        list.GetProperty("note").GetString().ShouldBe("later");
        var devices = list.GetProperty("devices").EnumerateArray().ToDictionary(device => device.GetProperty("endpoint").GetString()!);
        devices["https://fcm.googleapis.com/fcm/send/phone"].GetProperty("label").GetString().ShouldBe("Pixel");
        devices["https://fcm.googleapis.com/fcm/send/laptop"].TryGetProperty("label", out _).ShouldBeFalse();
    }
}
