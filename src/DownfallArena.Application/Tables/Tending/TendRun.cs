using System.Globalization;
using System.Security.Cryptography;

namespace DownfallArena.Application.Tables.Tending;

/// <summary>
/// One tending pass as it is kept for the admin panel's Jobs tab (docs/tabletop/admin-plan.md, A3): when it started
/// and ended, what started it and who, whether it finished, and what it did -- the tables that never began it closed,
/// the invitations it let go of, the games it ended because a player's time ran out (ADR 0169) or a left seat stayed
/// empty (ADR 0170), the games whose player it warned, and how many guests, notice logs and closed feedback it let go of
/// (#1249). The ids are the tables' sessions. A run kept before the counts were reads them as none.
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
    /// <summary>How many guests no browser could reach any more it removed (ADR 0207).</summary>
    public int GuestsRemoved { get; init; }

    /// <summary>How many accounts' notice logs it erased what was past 90 days from (ADR 0201).</summary>
    public int NoticesErased { get; init; }

    /// <summary>How much feedback closed 180 days ago it let go of (ADR 0189).</summary>
    public int FeedbackLetGo { get; init; }

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
