using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The shape an account store writes, the same in a directory and in a blob container, and where each record
/// is: <c>accounts/&lt;id&gt;.json</c>, <c>by-subject/&lt;provider&gt;/&lt;subject&gt;.json</c> and
/// <c>by-name/&lt;key&gt;.json</c>. A subject is written in base64url, because a provider chooses its characters
/// and a path must not let it.
/// </summary>
internal static class AccountDocuments
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public const string SessionKeyPath = "session.key";

    private const int SessionKeyBytes = 32;

    public static string NewSessionKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(SessionKeyBytes));

    public static byte[] ReadSessionKey(string text)
    {
        var key = Convert.FromBase64String(text.Trim());
        return key.Length >= SessionKeyBytes ? key : throw new InvalidDataException($"The session key holds {key.Length} bytes; it takes {SessionKeyBytes}.");
    }

    /// <summary>The prefix every account record is under, which a listing reads.</summary>
    public const string AccountsPrefix = "accounts/";

    public static string AccountPath(AccountId id) => $"{AccountsPrefix}{id.Value}.json";

    public static string SubjectPath(ExternalIdentity identity) =>
        $"by-subject/{identity.Provider}/{Convert.ToBase64String(Encoding.UTF8.GetBytes(identity.Subject)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}.json";

    public static string NamePath(Username username) => $"by-name/{username.Key}.json";

    public static string Write(Account account) => JsonSerializer.Serialize(
        new AccountRecord(account.Id.Value, account.Identity.Provider, account.Identity.Subject, account.Username.Value, account.CreatedAt, account.Generation),
        Web);

    public static Account ReadAccount(string json)
    {
        var record = JsonSerializer.Deserialize<AccountRecord>(json, Web) ?? throw new InvalidDataException("An account record is empty.");
        return new Account(
            AccountId.Parse(record.Id),
            new ExternalIdentity(record.Provider, record.Subject),
            Username.Restore(record.Username),
            record.CreatedAt,
            record.Generation);
    }

    public static string Write(UsernameReservation reservation) =>
        JsonSerializer.Serialize(new NameRecord(reservation.Username.Value, reservation.Account.Value, reservation.ReservedAt), Web);

    public static UsernameReservation ReadName(string json)
    {
        var record = JsonSerializer.Deserialize<NameRecord>(json, Web) ?? throw new InvalidDataException("A name record is empty.");
        return new UsernameReservation(Username.Restore(record.Username), AccountId.Parse(record.Account), record.ReservedAt);
    }

    public static string WriteSubject(AccountId account) => JsonSerializer.Serialize(new SubjectRecord(account.Value), Web);

    public static AccountId ReadSubject(string json)
    {
        var record = JsonSerializer.Deserialize<SubjectRecord>(json, Web) ?? throw new InvalidDataException("A sign-in record is empty.");
        return AccountId.Parse(record.Account);
    }

    private sealed record AccountRecord(string Id, string Provider, string Subject, string Username, DateTimeOffset CreatedAt, int Generation);

    private sealed record NameRecord(string Username, string Account, DateTimeOffset ReservedAt);

    private sealed record SubjectRecord(string Account);
}
