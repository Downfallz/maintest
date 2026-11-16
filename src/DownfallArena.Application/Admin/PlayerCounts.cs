using DownfallArena.Application.Accounts.Players;

namespace DownfallArena.Application.Admin;

/// <summary>
/// The players at a glance (headquarters A5): how many accounts, how many were seen and how many were made in the last
/// seven days, and how many the operator suspended (ADR 0178).
/// </summary>
public sealed record PlayerCounts(int Total, int SeenThisWeek, int NewThisWeek, int Suspended)
{
    public static PlayerCounts Of(IReadOnlyCollection<PlayerSummary> players, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(players);
        var week = now.AddDays(-7);
        return new PlayerCounts(
            players.Count,
            players.Count(player => player.Activity?.LastSeen >= week),
            players.Count(player => player.Account.CreatedAt >= week),
            players.Count(player => player.Account.Suspension is not null));
    }
}
