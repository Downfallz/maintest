using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// One account's finished games of one kind, newest first, a page at a time (ADR 0147): what the lobby's Finished
/// tab lists. Read from the kept results, so a finished game outlives the hour a host keeps its table and every
/// restart. Against bots is any game a bot played in; against players, every other one, a shared device's included.
/// </summary>
public static class FinishedGames
{
    public const int PageSize = 6;

    public static FinishedPage Page(IEnumerable<MatchResult> results, AccountId account, bool againstBots, int page, int size = PageSize)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        MatchResult[] mine = [.. results
            .Where(result => result.Seats.Any(seat => seat.Account == account) && result.AgainstBot == againstBots)
            .OrderByDescending(result => result.FinishedAt)
            .ThenBy(result => result.MatchId, StringComparer.Ordinal)];
        var pages = Math.Max(1, (mine.Length + size - 1) / size);
        var at = Math.Clamp(page, 0, pages - 1);
        return new FinishedPage(mine.Length, pages, at, [.. mine.Skip(at * size).Take(size)]);
    }
}
