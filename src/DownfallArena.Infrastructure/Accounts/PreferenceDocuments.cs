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

    /// <summary>The folder every account's documents are kept under.</summary>
    public const string Prefix = "preferences/";

    public static string PathOf(AccountId account) => $"preferences/{account.Value}.json";

    /// <summary>The account a document's name, without its folder, is kept for; null for any other name (a staged write).</summary>
    public static AccountId? AccountOf(string name)
    {
        // The longest ending first: a Tutorials document's name also ends in ".json".
        foreach (var ending in (string[])[".tutorials.json", ".notices.json", ".json"])
        {
            if (name.EndsWith(ending, StringComparison.Ordinal))
            {
                return AccountId.TryParse(name[..^ending.Length], out var account) ? account : null;
            }
        }

        return null;
    }

    /// <summary>The account's Tutorials done and skipped (ADR 0187), beside its settings.</summary>
    public static string TutorialsPathOf(AccountId account) => $"preferences/{account.Value}.tutorials.json";

    /// <summary>What pops up on the account's devices (ADR 0202), beside its settings.</summary>
    public static string NoticesPathOf(AccountId account) => $"preferences/{account.Value}.notices.json";

    public static string WriteNotices(NoticePreferences notices) => JsonSerializer.Serialize(new NoticesRecord(notices.Turns, notices.Invitations, notices.Results), Web);

    /// <summary>The choices a document says; a kind it does not name pops up, and one that is not JSON reads as every kind.</summary>
    public static NoticePreferences ReadNotices(string json)
    {
        try
        {
            var record = JsonSerializer.Deserialize<NoticesRecord>(json, Web);
            return record is null ? NoticePreferences.Default : new NoticePreferences(record.Turns ?? true, record.Invitations ?? true, record.Results ?? true);
        }
        catch (JsonException)
        {
            return NoticePreferences.Default;
        }
    }

    public static string WriteTutorials(TutorialProgress tutorials) => JsonSerializer.Serialize(new TutorialsRecord(tutorials.Done, tutorials.Skipped), Web);

    /// <summary>The progress a document says; one that is not JSON reads as none, so the account starts again as new.</summary>
    public static TutorialProgress ReadTutorials(string json)
    {
        try
        {
            var record = JsonSerializer.Deserialize<TutorialsRecord>(json, Web);
            return TutorialProgress.Of(record?.Done, record?.Skipped);
        }
        catch (JsonException)
        {
            return TutorialProgress.None;
        }
    }

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

    private sealed record NoticesRecord(bool? Turns, bool? Invitations, bool? Results);

    private sealed record TutorialsRecord(IReadOnlyList<string>? Done, IReadOnlyList<string>? Skipped);
}
