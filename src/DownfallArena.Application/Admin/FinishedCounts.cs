using DownfallArena.Application.Accounts;

namespace DownfallArena.Application.Admin;

/// <summary>
/// The finished games at a glance (headquarters A5): those that count, finished in the last day and the last seven,
/// and those voided in the last seven (ADR 0176), by when they were voided: a game finished long ago can be voided
/// today. From the results kept for accounts.
/// </summary>
public sealed record FinishedCounts(int LastDay, int LastWeek, int VoidedThisWeek)
{
    public static FinishedCounts Of(IEnumerable<MatchResult> results, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(results);
        var all = results.ToArray();
        var counting = all.Where(result => result.Voided is null).ToArray();
        return new FinishedCounts(
            counting.Count(result => result.FinishedAt >= now.AddDays(-1)),
            counting.Count(result => result.FinishedAt >= now.AddDays(-7)),
            all.Count(result => result.Voided?.At >= now.AddDays(-7)));
    }
}
