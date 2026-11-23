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
/// </summary>
internal static class DeviceDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string PathOf(AccountId account) => $"devices/{account.Value}.json";

    public static string Write(IReadOnlyList<PushDevice> devices) =>
        JsonSerializer.Serialize(new DevicesRecord([.. devices.Select(device => new DeviceRecord(device.Endpoint, device.P256dh, device.Auth, device.AddedAt, device.Gone))]), Web);

    public static IReadOnlyList<PushDevice> Read(string json)
    {
        var record = JsonSerializer.Deserialize<DevicesRecord>(json, Web) ?? throw new InvalidDataException("A devices record is empty.");
        // An entry without keys is gone whether or not it says so: an image from before ADR 0203 drops the flag when
        // it writes the list over, and a device without keys could never be told anyway.
        return [.. (record.Devices ?? []).Select(device => new PushDevice(device.Endpoint, device.P256dh ?? string.Empty, device.Auth ?? string.Empty, device.AddedAt,
            device.Gone || string.IsNullOrEmpty(device.P256dh) || string.IsNullOrEmpty(device.Auth)))];
    }

    private sealed record DevicesRecord(IReadOnlyList<DeviceRecord>? Devices);

    private sealed record DeviceRecord(string Endpoint, string? P256dh, string? Auth, DateTimeOffset AddedAt, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Gone = false);
}
