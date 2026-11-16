using System.Globalization;
using System.Security.Cryptography;

namespace DownfallArena.Application.Tables.Tending;

/// <summary>
/// One tending pass as it is kept for the admin panel's Jobs tab (docs/tabletop/admin-plan.md, A3): when it started
/// and ended, what started it and who, whether it finished, and what it did -- the tables that never began it closed,
/// the invitations it let go of, the games it ended because a player's time ran out (ADR 0169) or a left seat stayed
/// empty (ADR 0170), and the games whose player it warned. The ids are the tables' sessions.
/// </summary>
public sealed record TendRun(
    string Id,
    DateTimeOffset Started,
    DateTimeOffset Finished,
    TendTrigger Trigger,
    string? By,
    TendOutcome Outcome,
    string? Error,
    IReadOnlyList<string> NeverBegan,
    int InvitationsExpired,
    IReadOnlyList<string> TimedOut,
    IReadOnlyList<string> Abandoned,
    IReadOnlyList<string> Warned)
{
    /// <summary>How many runs are kept: the oldest go past it, about three weeks of the hourly job.</summary>
    public const int Kept = 500;

    private const string StartedFormat = "yyyyMMdd'T'HHmmssfff'Z'";

    /// <summary>
    /// An id for a run started then: the start time first, to the millisecond, so ids sort as the runs started, and a
    /// few random characters after it so two passes started in the same millisecond keep two records.
    /// </summary>
    public static string NewId(DateTimeOffset started) =>
        $"{started.UtcDateTime.ToString(StartedFormat, CultureInfo.InvariantCulture)}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3))}";

    /// <summary>When the run an id names started, read off the id alone; null for an id this did not make.</summary>
    public static DateTimeOffset? StartedOf(string? id) =>
        id is { Length: > 19 } && DateTimeOffset.TryParseExact(id[..19], StartedFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var started)
            ? started
            : null;

    /// <summary>Whether an id is one a run could have: what a route checks before it names a file or a blob after it.</summary>
    public static bool IsId(string? id) =>
        StartedOf(id) is not null && id!.Length == 26 && id[19] == '-' && id[20..].All(char.IsAsciiHexDigitLower);
}
