using System.Text.Json;
using DownfallArena.Application.Hosting;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The shape a host settings store writes (ADR 0177), the same in a directory and in a blob container: one document
/// for the host at <c>settings/host.json</c>, beside the accounts.
/// </summary>
internal static class HostSettingsDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public const string Path = "settings/host.json";

    public static string Write(HostSettings settings) =>
        JsonSerializer.Serialize(new SettingsRecord(settings.RegistrationOpen, settings.Notice, settings.UpdatedBy, settings.UpdatedAt), Web);

    /// <summary>The settings a document holds; a field it lacks reads as the default, registration open.</summary>
    public static HostSettings? Read(string json) =>
        JsonSerializer.Deserialize<SettingsRecord>(json, Web) is { } record
            ? new HostSettings(record.RegistrationOpen ?? true, string.IsNullOrWhiteSpace(record.Notice) ? null : record.Notice, record.UpdatedBy, record.UpdatedAt)
            : null;

    private sealed record SettingsRecord(bool? RegistrationOpen, string? Notice, string? UpdatedBy, DateTimeOffset? UpdatedAt);
}
