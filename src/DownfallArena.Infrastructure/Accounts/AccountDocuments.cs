using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The shape an account store writes, the same in a directory and in a blob container, and where each record
/// is (each identity of an account has its own <c>by-subject</c> entry, ADR 0204): <c>accounts/&lt;id&gt;.json</c>, <c>by-subject/&lt;provider&gt;/&lt;subject&gt;.json</c> and
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
        new AccountRecord(account.Id.Value, account.Identities.All[0].Provider, account.Identities.All[0].Subject, account.Username.Value, account.CreatedAt, account.Generation)
        {
            Identities = [.. account.Identities.All.Select(identity => new IdentityRecord(identity.Provider, identity.Subject))],
            Suspension = account.Suspension is { } suspension
                ? new SuspensionRecord(suspension.Reason, suspension.By, suspension.At)
                : null,
            Unbinding = account.Unbinding is { } unbinding ? new IdentityRecord(unbinding.Provider, unbinding.Subject) : null,
        },
        Web);

    public static Account ReadAccount(string json)
    {
        var record = JsonSerializer.Deserialize<AccountRecord>(json, Web) ?? throw new InvalidDataException("An account record is empty.");
        return new Account(
            AccountId.Parse(record.Id),
            ReadIdentities(record),
            Username.Restore(record.Username),
            record.CreatedAt,
            record.Generation)
        {
            Suspension = record.Suspension is { } suspension
                ? new AccountSuspension(suspension.Reason, suspension.By, suspension.At)
                : null,
            Unbinding = record.Unbinding is { } unbinding ? new ExternalIdentity(unbinding.Provider, unbinding.Subject) : null,
        };
    }

    // A record written before ADR 0204 holds one identity as provider and subject; a later one also lists them all.
    // A broken list is an InvalidDataException, as every other broken record is, whichever check refuses it.
    private static AccountIdentities ReadIdentities(AccountRecord record)
    {
        try
        {
            return record.Identities is { Count: > 0 } identities
                ? AccountIdentities.Restore(identities.Select((identity, index) => identity is null
                    ? throw new InvalidDataException($"Account {record.Id}: identities[{index}] is null.")
                    : new ExternalIdentity(identity.Provider, identity.Subject)))
                : AccountIdentities.Of(new ExternalIdentity(record.Provider, record.Subject));
        }
        catch (ArgumentException broken)
        {
            throw new InvalidDataException($"Account {record.Id}: its identities do not read.", broken);
        }
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

    // Suspension is missing from a record written before ADR 0178, and from every account in good standing.
    // Identities is missing from a record written before ADR 0204; Provider and Subject stay the first identity, so
    // the image before it still reads an account written after (the way back). Unbinding is missing but while a
    // removal is between its writes (#1216); an image before it ignores it, and the binding waits for the next removal.
    private sealed record AccountRecord(string Id, string Provider, string Subject, string Username, DateTimeOffset CreatedAt, int Generation)
    {
        public SuspensionRecord? Suspension { get; init; }

        public IReadOnlyList<IdentityRecord?>? Identities { get; init; }

        public IdentityRecord? Unbinding { get; init; }
    }

    private sealed record IdentityRecord(string Provider, string Subject);

    private sealed record SuspensionRecord(string Reason, string By, DateTimeOffset At);

    private sealed record NameRecord(string Username, string Account, DateTimeOffset ReservedAt);

    private sealed record SubjectRecord(string Account);
}
