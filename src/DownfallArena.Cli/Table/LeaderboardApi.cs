using System.Text.Json;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Leaderboard;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The ladder (ADR 0124): every account with a ranked game, by Glicko-2 rating, named by the name its account chose,
/// and, for whoever is signed in, their own place and their record against each bot. Read by anybody, signed in or
/// not. The ladder is computed from the kept results and held for half a minute, so a page left open costs one read
/// of the results a half-minute, however many people have it open.
/// </summary>
internal sealed class LeaderboardApi(IMatchResults results, AccountApi accounts, TimeProvider clock, Func<string, string>? botName = null)
{
    public const string Path = "/api/leaderboard";

    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private (Standings Standings, DateTimeOffset At)? _cached;

    public static bool Names(string path) => path == Path;

    public async Task<StudioResponse> HandleAsync(string method, AccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (method != "GET")
        {
            return StudioResponse.OfJson(new { error = "Leaderboard.NoRoute", message = "The leaderboard is read, not written." }, Web, 405);
        }

        var standings = await StandingsAsync();
        var who = await accounts.WhoAsync(request);
        var players = new List<object>();
        object? you = null;
        foreach (var standing in standings.Players)
        {
            // A deleted account leaves the ladder: its games still rated whoever played it, but it has no name to show.
            if (await accounts.AccountAsync(standing.Account) is not { } account)
            {
                continue;
            }

            var mine = who?.Account.Id == standing.Account;
            var row = Row(players.Count + 1, account.Username.Value, standing, mine);
            players.Add(row);
            if (mine)
            {
                you = row;
            }
        }

        var bots = who is null
            ? []
            // By the name the player knows the bot by, its level (ADR 0180): two bots that were Hard one after the other are one row.
            : standings.AgainstBots(who.Account.Id).GroupBy(record => botName?.Invoke(record.Agent) ?? record.Agent, StringComparer.Ordinal)
                .Select(bot => new { bot = bot.Key, Games = bot.Sum(record => record.Games), Wins = bot.Sum(record => record.Wins), Losses = bot.Sum(record => record.Losses), Draws = bot.Sum(record => record.Draws) }).ToArray();
        var ladder = StudioResponse.OfJson(
            new
            {
                provisionalAbove = Standing.ProvisionalDeviation,
                players,
                you = who is null ? null : new { username = who.Account.Username.Value, standing = you, bots },
            },
            Web);

        // A player who only ever comes back to the ladder keeps their session, as one who opens the lobby does.
        return who?.Renewed is { } renewed ? ladder with { Headers = [new KeyValuePair<string, string>("Set-Cookie", renewed)] } : ladder;
    }

    private static object Row(int rank, string username, Standing standing, bool mine) => new
    {
        rank,
        username,
        rating = (int)Math.Round(standing.Rating.Rating),
        deviation = (int)Math.Round(standing.Rating.Deviation),
        standing.Games,
        standing.Wins,
        standing.Losses,
        standing.Draws,
        standing.Provisional,
        lastPlayed = standing.LastPlayed,
        you = mine,
    };

    private async Task<Standings> StandingsAsync()
    {
        var now = clock.GetUtcNow();
        if (_cached is { } held && now - held.At < CacheFor)
        {
            return held.Standings;
        }

        // Two requests arriving as the cache runs out may both compute it; the ladder is the same either way.
        var standings = Standings.Compute(await results.ListAsync(), now);
        _cached = (standings, now);
        return standings;
    }
}
