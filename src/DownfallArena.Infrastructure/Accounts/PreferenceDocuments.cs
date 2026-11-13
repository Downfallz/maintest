using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The shape a preference store writes, the same in a directory and in a blob container: one document an account at
/// <c>preferences/&lt;accountId&gt;.json</c>. A document the settings no longer offer reads as none, so the account
/// starts again from the defaults rather than failing.
/// </summary>
internal static class PreferenceDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string PathOf(AccountId account) => $"preferences/{account.Value}.json";

    public static string Write(GamePreferences preferences) => JsonSerializer.Serialize(
        new PreferencesRecord(preferences.Pace.ToString().ToLowerInvariant(), preferences.Speed, preferences.Help, preferences.ReducedMotion, preferences.Reactions), Web);

    public static GamePreferences? Read(string json)
    {
        var record = JsonSerializer.Deserialize<PreferencesRecord>(json, Web);
        if (record is null)
        {
            return null;
        }

        var made = GamePreferences.Create(record.Pace, record.Speed, record.Help, record.ReducedMotion, record.Reactions ?? true);
        return made.IsSuccess ? made.Value : null;
    }

    // Reactions is missing from a document written before ADR 0173, which shows them, as every account did then.
    private sealed record PreferencesRecord(string? Pace, decimal Speed, bool Help, bool ReducedMotion, bool? Reactions);
}
