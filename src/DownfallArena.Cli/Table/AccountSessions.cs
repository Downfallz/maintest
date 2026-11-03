using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The host's own proof of who is signed in (ADR 0122): a session, kept in an <c>HttpOnly</c> cookie, and a ticket,
/// the ten minutes between Google saying who someone is and that person choosing a name. Both are text signed with
/// the host's session key (HMAC-SHA256), each under its own prefix so one can never be read as the other.
/// A session names the account and the generation it was issued under: raising the account's generation is what
/// refuses every session issued before.
/// </summary>
internal sealed class AccountSessions
{
    public const string Cookie = "downfall_session";

    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>A session older than this is issued again when it is used, so a player who comes back is not signed out.</summary>
    public static readonly TimeSpan RenewAfter = TimeSpan.FromDays(1);

    public static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(10);

    private const string SessionPrefix = "s1";
    private const string TicketPrefix = "t1";
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(1);

    private readonly byte[] _key;
    private readonly TimeProvider _clock;

    public AccountSessions(byte[] key, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(clock);
        if (key.Length < 32)
        {
            throw new ArgumentException("A session key holds at least 32 bytes.", nameof(key));
        }

        _key = key;
        _clock = clock;
    }

    /// <summary>The host's clock, which every age a session or a cached account has is measured by.</summary>
    public TimeProvider Clock => _clock;

    public string Issue(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return Sign(string.Join('.', SessionPrefix, account.Id.Value, Seconds(_clock.GetUtcNow()), account.Generation.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>What a session cookie says, or null when it is not one this host signed, or is too old.</summary>
    public SessionClaim? Read(string? value)
    {
        if (Verified(value, SessionPrefix) is not [var id, var issued, var generation]
            || !AccountId.TryParse(id, out var account)
            || !long.TryParse(issued, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || !int.TryParse(generation, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(Math.Min(seconds, DateTimeOffset.MaxValue.ToUnixTimeSeconds()));
        var age = _clock.GetUtcNow() - issuedAt;
        return age >= Lifetime || age < -Skew ? null : new SessionClaim(account, issuedAt, number);
    }

    public bool DueForRenewal(SessionClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return _clock.GetUtcNow() - claim.IssuedAt >= RenewAfter;
    }

    public string Ticket(ExternalIdentity identity) => Sign(string.Join(
        '.',
        TicketPrefix,
        identity.Provider,
        Base64Url(Encoding.UTF8.GetBytes(identity.Subject)),
        Seconds(_clock.GetUtcNow() + TicketLifetime)));

    /// <summary>The sign-in a ticket holds, or null when it is not one this host signed, or has run out.</summary>
    public ExternalIdentity? ReadTicket(string? value)
    {
        if (Verified(value, TicketPrefix) is not [var provider, var subject, var expires]
            || !long.TryParse(expires, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds <= _clock.GetUtcNow().ToUnixTimeSeconds())
        {
            return null;
        }

        try
        {
            return new ExternalIdentity(provider, Encoding.UTF8.GetString(FromBase64Url(subject)));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The <c>Set-Cookie</c> that keeps a session; <c>Secure</c> everywhere but a loopback host.</summary>
    public static string SetCookie(string session, bool secure) =>
        $"{Cookie}={session}; Path=/; Max-Age={(int)Lifetime.TotalSeconds}; HttpOnly; SameSite=Lax{(secure ? "; Secure" : "")}";

    public static string ClearCookie(bool secure) => $"{Cookie}=; Path=/; Max-Age=0; HttpOnly; SameSite=Lax{(secure ? "; Secure" : "")}";

    private static string Seconds(DateTimeOffset at) => at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    private string Sign(string payload) => $"{payload}.{Base64Url(Mac(payload))}";

    private byte[] Mac(string payload) => HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));

    /// <summary>The fields after the prefix of a value this host signed, or null; the comparison takes fixed time.</summary>
    private string[]? Verified(string? value, string prefix)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 512)
        {
            return null;
        }

        var cut = value.LastIndexOf('.');
        if (cut <= 0)
        {
            return null;
        }

        var payload = value[..cut];
        byte[] mac;
        try
        {
            mac = FromBase64Url(value[(cut + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(mac, Mac(payload)))
        {
            return null;
        }

        var fields = payload.Split('.');
        return fields[0] == prefix ? fields[1..] : null;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var standard = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(standard.PadRight(standard.Length + ((4 - (standard.Length % 4)) % 4), '='));
    }
}
