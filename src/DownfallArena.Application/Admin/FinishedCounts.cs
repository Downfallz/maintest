using DownfallArena.Application.Accounts;

namespace DownfallArena.Application.Admin;

/// <summary>
/// The finished games at a glance (headquarters A5): those that count, finished in the last day and the last seven,
/// and those voided in the last seven (ADR 0176), from the results kept for accounts.
/// </summary>
public sealed record FinishedCounts(int LastDay, int LastWeek, int VoidedThisWeek)
{
    public static FinishedCounts Of(IEnumerable<MatchResult> results, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(results);
        var week = results.Where(result => result.FinishedAt >= now.AddDays(-7)).ToArray();
        return new FinishedCounts(
            week.Count(result => result.Voided is null && result.FinishedAt >= now.AddDays(-1)),
            week.Count(result => result.Voided is null),
            week.Count(result => result.Voided is not null));
    }
}
