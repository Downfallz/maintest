using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Leaderboard;

/// <summary>
/// The ladder read from the kept results (ADR 0124). Ranked games, oldest first, each a rating period of its own
/// for its two players, rated against each other as they stood before it; a day without a game grows a player's
/// deviation by one idle period, up to the day asked about. Games against a bot are tallied per bot and never rated.
/// </summary>
public sealed class Standings
{
    private readonly Dictionary<AccountId, IReadOnlyList<BotRecord>> _bots;

    private Standings(IReadOnlyList<Standing> players, Dictionary<AccountId, IReadOnlyList<BotRecord>> bots)
    {
        Players = players;
        _bots = bots;
    }

    /// <summary>Everyone with a ranked game: established players by rating, then provisional ones by rating.</summary>
    public IReadOnlyList<Standing> Players { get; }

    public static Standings Compute(IEnumerable<MatchResult> results, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(results);
        var ratings = new Dictionary<AccountId, (GlickoRating Rating, int Wins, int Losses, int Draws, DateTimeOffset Last)>();
        var bots = new Dictionary<(AccountId, string), (int Wins, int Losses, int Draws)>();

        foreach (var result in results.OrderBy(result => result.FinishedAt).ThenBy(result => result.MatchId, StringComparer.Ordinal))
        {
            if (result.Ranked)
            {
                Rate(ratings, result);
            }
            else if (AgainstABot(result) is { } bot)
            {
                var key = (bot.Account, bot.Agent);
                var tally = bots.GetValueOrDefault(key);
                bots[key] = Tally(tally, bot.Points);
            }
        }

        var players = ratings
            .Select(entry => new Standing(entry.Key, Glicko2.Idle(entry.Value.Rating, Days(entry.Value.Last, now)), entry.Value.Wins, entry.Value.Losses, entry.Value.Draws, entry.Value.Last))
            .OrderBy(standing => standing.Provisional)
            .ThenByDescending(standing => standing.Rating.Rating)
            .ThenBy(standing => standing.Account.Value, StringComparer.Ordinal)
            .ToList();
        var byAccount = bots
            .GroupBy(entry => entry.Key.Item1)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<BotRecord>)[.. group.Select(entry => new BotRecord(entry.Key.Item2, entry.Value.Wins, entry.Value.Losses, entry.Value.Draws)).OrderBy(record => record.Agent, StringComparer.Ordinal)]);
        return new Standings(players, byAccount);
    }

    /// <summary>The account's games against each bot, by the bot's spec; empty for none.</summary>
    public IReadOnlyList<BotRecord> AgainstBots(AccountId account) => _bots.GetValueOrDefault(account) ?? [];

    private static void Rate(Dictionary<AccountId, (GlickoRating Rating, int Wins, int Losses, int Draws, DateTimeOffset Last)> ratings, MatchResult result)
    {
        var first = result.Seats[0].Account!.Value;
        var second = result.Seats[1].Account!.Value;
        var a = Before(ratings, first, result.FinishedAt);
        var b = Before(ratings, second, result.FinishedAt);
        var pointsA = Points(result, result.Seats[0].Seat);

        var ratedA = Glicko2.Rate(a.Rating, [(b.Rating, pointsA / 2.0)]);
        var ratedB = Glicko2.Rate(b.Rating, [(a.Rating, (2 - pointsA) / 2.0)]);
        ratings[first] = (ratedA, a.Wins + (pointsA == 2 ? 1 : 0), a.Losses + (pointsA == 0 ? 1 : 0), a.Draws + (pointsA == 1 ? 1 : 0), result.FinishedAt);
        ratings[second] = (ratedB, b.Wins + (pointsA == 0 ? 1 : 0), b.Losses + (pointsA == 2 ? 1 : 0), b.Draws + (pointsA == 1 ? 1 : 0), result.FinishedAt);
    }

    /// <summary>A player as they stood just before a game: unrated if new, and less certain for every day since their last.</summary>
    private static (GlickoRating Rating, int Wins, int Losses, int Draws, DateTimeOffset Last) Before(
        Dictionary<AccountId, (GlickoRating Rating, int Wins, int Losses, int Draws, DateTimeOffset Last)> ratings,
        AccountId account,
        DateTimeOffset at) =>
        ratings.TryGetValue(account, out var known)
            ? known with { Rating = Glicko2.Idle(known.Rating, Days(known.Last, at)) }
            : (GlickoRating.Unrated, 0, 0, 0, at);

    private static int Days(DateTimeOffset from, DateTimeOffset to) => Math.Max(0, (int)(to - from).TotalDays);

    /// <summary>A seat's half-points in a result: 2 for a win, 1 for a draw, 0 for a loss.</summary>
    private static int Points(MatchResult result, string seat) => result.Winner is null ? 1 : result.Winner == seat ? 2 : 0;

    /// <summary>One account against one bot, and the account's half-points, or null for any other shape of game.</summary>
    private static (AccountId Account, string Agent, int Points)? AgainstABot(MatchResult result)
    {
        if (result.Seats.Count != 2)
        {
            return null;
        }

        var person = result.Seats.FirstOrDefault(seat => seat.Account is not null);
        var bot = result.Seats.FirstOrDefault(seat => seat.Agent is not null);
        if (person?.Account is not { } account || bot?.Agent is not { } agent)
        {
            return null;
        }

        return (account, agent, Points(result, person.Seat));
    }

    private static (int Wins, int Losses, int Draws) Tally((int Wins, int Losses, int Draws) tally, int points) => points switch
    {
        2 => tally with { Wins = tally.Wins + 1 },
        0 => tally with { Losses = tally.Losses + 1 },
        _ => tally with { Draws = tally.Draws + 1 },
    };
}
