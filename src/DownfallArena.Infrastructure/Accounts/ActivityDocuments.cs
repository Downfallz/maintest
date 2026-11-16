using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The shape an activity store writes (ADR 0174), the same in a directory and in a blob container: one document an
/// account at <c>activity/&lt;accountId&gt;.json</c>, beside the account.
/// </summary>
internal static class ActivityDocuments
{
    public const string Prefix = "activity/";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string PathOf(AccountId account) => $"{Prefix}{account.Value}.json";

    public static string Write(AccountActivity activity) =>
        JsonSerializer.Serialize(new ActivityRecord(activity.Account.Value, activity.LastSignIn, activity.LastSeen, activity.FeedbackMute), Web);

    public static AccountActivity Read(string json)
    {
        var record = JsonSerializer.Deserialize<ActivityRecord>(json, Web) ?? throw new InvalidDataException("An activity record is empty.");
        return new AccountActivity(AccountId.Parse(record.Account), record.LastSignIn, record.LastSeen) { FeedbackMute = record.FeedbackMute };
    }

    // The mute from feedback is written only while the account has one, so a record from before it reads the same.
    private sealed record ActivityRecord(
        string Account,
        DateTimeOffset? LastSignIn,
        DateTimeOffset? LastSeen,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] FeedbackMute? FeedbackMute = null);
}
