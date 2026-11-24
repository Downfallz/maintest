using System.Text.Json;
using System.Text.Json.Serialization;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The shape a device store writes, the same in a directory and in a blob container: one document an account at
/// <c>devices/&lt;accountId&gt;.json</c>, holding its list. The keys are written as the browser gave them: the
/// store is the host's own (ADR 0080), and a key the host could not read back is a device it could not tell. A gone
/// endpoint (ADR 0203) is written with <c>"gone": true</c> and empty keys; a device that is not gone has no such field.
/// A field this revision does not know, on the list or on a device, is written back as it was read: during a deploy the
/// revision before runs beside the next, and would otherwise wipe what the next one added (#1211).
/// </summary>
internal static class DeviceDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string PathOf(AccountId account) => $"devices/{account.Value}.json";

    /// <summary>
    /// The devices as a document, keeping what <paramref name="before" />, the document they were read from, holds that
    /// this revision does not know: the fields of the list, and those of each device, matched by its endpoint.
    /// </summary>
    public static string Write(IReadOnlyList<PushDevice> devices, string? before = null)
    {
        ArgumentNullException.ThrowIfNull(devices);
        var kept = before is null ? null : Parse(before);
        var unknown = (kept?.Devices ?? [])
            .Where(device => device?.Unknown is { Count: > 0 } && device.Endpoint is not null)
            .GroupBy(device => device!.Endpoint, StringComparer.Ordinal)
            .ToDictionary(device => device.Key, device => device.First()!.Unknown, StringComparer.Ordinal);
        return JsonSerializer.Serialize(new DevicesRecord([.. devices.Select(device =>
            new DeviceRecord(device.Endpoint, device.P256dh, device.Auth, device.AddedAt, device.Gone) { Unknown = unknown.GetValueOrDefault(device.Endpoint) })])
        { Unknown = kept?.Unknown }, Web);
    }

    public static IReadOnlyList<PushDevice> Read(string json)
    {
        var record = Parse(json);
        // An entry without keys is gone whether or not it says so: an image from before ADR 0203 drops the flag when
        // it writes the list over, and a device without keys could never be told anyway.
        return [.. (record.Devices ?? []).Select(device => new PushDevice(device.Endpoint, device.P256dh ?? string.Empty, device.Auth ?? string.Empty, device.AddedAt,
            device.Gone || string.IsNullOrEmpty(device.P256dh) || string.IsNullOrEmpty(device.Auth)))];
    }

    private static DevicesRecord Parse(string json) =>
        JsonSerializer.Deserialize<DevicesRecord>(json, Web) ?? throw new InvalidDataException("A devices record is empty.");

    private sealed record DevicesRecord(IReadOnlyList<DeviceRecord>? Devices)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; init; }
    }

    private sealed record DeviceRecord(string Endpoint, string? P256dh, string? Auth, DateTimeOffset AddedAt, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Gone = false)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Unknown { get; init; }
    }
}
