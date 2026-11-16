using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// The players an account has met most recently, one entry each, the last met first: what the lobby offers to
/// challenge again. Read from the kept results (ADR 0147), so a game outlives its table. Only games between this
/// account and another account count: a bot, a seat nobody owned and both seats on one device have nobody to
/// challenge. A game the operator voided (ADR 0176) counts in no record.
/// </summary>
public static class RecentOpponents
{
    public const int MostShown = 8;

    public static IReadOnlyList<RecentOpponent> Of(IEnumerable<MatchResult> results, AccountId account, int most = MostShown)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentOutOfRangeException.ThrowIfLessThan(most, 1);
        var met = new List<(AccountId Other, string Mine, MatchResult Result)>();
        foreach (var result in results)
        {
            var mine = result.Seats.Where(seat => seat.Account == account).ToArray();
            var other = result.Seats.FirstOrDefault(seat => seat.Account is { } id && id != account)?.Account;
            if (mine.Length == 1 && other is { } opponent && !result.AgainstBot && result.Voided is null)
            {
                met.Add((opponent, mine[0].Seat, result));
            }
        }

        return [.. met
            .GroupBy(game => game.Other)
            .Select(Summary)
            .OrderByDescending(opponent => opponent.LastPlayed)
            .ThenBy(opponent => opponent.Account.Value, StringComparer.Ordinal)
            .Take(most)];
    }

    private static RecentOpponent Summary(IGrouping<AccountId, (AccountId Other, string Mine, MatchResult Result)> games)
    {
        var last = games.MaxBy(game => game.Result.FinishedAt).Result;
        var won = games.Count(game => game.Result.Winner == game.Mine);
        var drawn = games.Count(game => game.Result.Winner is null);
        var count = games.Count();
        return new RecentOpponent(games.Key, last.FinishedAt, count, won, count - won - drawn, drawn, last.Format, last.Casual);
    }
}
