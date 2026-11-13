using System.Text.Json;
using DownfallArena.Application.Accounts;
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
        JsonSerializer.Serialize(new ActivityRecord(activity.Account.Value, activity.LastSignIn, activity.LastSeen), Web);

    public static AccountActivity Read(string json)
    {
        var record = JsonSerializer.Deserialize<ActivityRecord>(json, Web) ?? throw new InvalidDataException("An activity record is empty.");
        return new AccountActivity(AccountId.Parse(record.Account), record.LastSignIn, record.LastSeen);
    }

    private sealed record ActivityRecord(string Account, DateTimeOffset? LastSignIn, DateTimeOffset? LastSeen);
}
