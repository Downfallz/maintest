using System.Text.Json;
using DownfallArena.Application.Hosting;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The operator's Settings tab (ADR 0177, admin plan A4): the host's settings read and replaced whole, stamped with who
/// saved them and when. Reached through <see cref="AdminApi" />, behind its gate.
/// </summary>
internal sealed class SettingsApi(HostSettingsCache settings, TimeProvider clock)
{
    public const string Route = "/api/admin/settings";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static bool Names(string path) => path == Route;

    public async Task<StudioResponse> HandleAsync(string method, string body, string operatorName) => method switch
    {
        "GET" => Answer(await settings.CurrentAsync()),
        "PUT" => await SaveAsync(body, operatorName),
        _ => Problem(405, "Settings.NoRoute", $"No such route: {method} {Route}"),
    };

    private async Task<StudioResponse> SaveAsync(string body, string operatorName)
    {
        SettingsBody? asked;
        try
        {
            asked = string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<SettingsBody>(body, Web);
        }
        catch (JsonException)
        {
            asked = null;
        }

        if (asked?.RegistrationOpen is not { } open)
        {
            return Problem(400, "Settings.BadRequest", "Send the settings whole: registrationOpen, true or false, and a notice, or none.");
        }

        var saved = HostSettings.Create(open, asked.Notice, operatorName, clock.GetUtcNow());
        if (saved.IsFailure)
        {
            return Problem(400, saved.Error.Code, saved.Error.Message);
        }

        await settings.SaveAsync(saved.Value);
        Console.WriteLine($"  Host settings saved by {operatorName}: registration {(open ? "open" : "closed")}, {(saved.Value.Notice is null ? "no notice" : $"notice \"{saved.Value.Notice}\"")}.");
        return Answer(saved.Value);
    }

    /// <summary>The settings as the panel reads them.</summary>
    private static StudioResponse Answer(HostSettings settings) => StudioResponse.OfJson(
        new { settings = new { settings.RegistrationOpen, settings.Notice, settings.UpdatedBy, settings.UpdatedAt }, longestNotice = HostSettings.LongestNotice },
        Web);

    private static StudioResponse Problem(int status, string code, string message) =>
        StudioResponse.OfJson(new { error = code, message }, Web, status);

    private sealed record SettingsBody(bool? RegistrationOpen, string? Notice);
}
