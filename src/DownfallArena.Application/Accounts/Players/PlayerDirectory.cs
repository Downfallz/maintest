using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Leaderboard;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Players;

/// <summary>
/// The operator's list of players (ADR 0174), read whole from what is already kept and never kept twice: every
/// account, its activity, its finished games and the ladder. Sorting by games or by a visit needs every account read
/// once, which a host of tens to hundreds of accounts affords.
/// </summary>
public sealed class PlayerDirectory
{
    public const int PerPage = 20;

    private readonly IAccountStore _accounts;
    private readonly AccountActivityLog _activity;
    private readonly IMatchResults? _results;
    private readonly TimeProvider _clock;

    /// <param name="results">The kept results, or none on a host that keeps none: every count is then zero.</param>
    public PlayerDirectory(IAccountStore accounts, AccountActivityLog activity, IMatchResults? results, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(clock);
        _accounts = accounts;
        _activity = activity;
        _results = results;
        _clock = clock;
    }

    /// <summary>Every account with its activity, its games and its standing, in no particular order.</summary>
    public async Task<IReadOnlyList<PlayerSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _accounts.ListAsync(cancellationToken);
        var activity = (await _activity.ListAsync(cancellationToken)).ToDictionary(kept => kept.Account);
        var results = _results is null ? [] : await _results.ListAsync(cancellationToken);
        var standings = Standings.Compute(results, _clock.GetUtcNow()).Players.ToDictionary(standing => standing.Account);
        var played = results
            .SelectMany(result => result.Seats.Where(seat => seat.Account is not null).Select(seat => (Account: seat.Account!.Value, Seat: seat, Result: result)))
            .ToLookup(entry => entry.Account);
        return [.. accounts.Select(account => Summarize(account, activity.GetValueOrDefault(account.Id), played[account.Id], standings.GetValueOrDefault(account.Id)))];
    }

    /// <summary>One account as the list shows it, or null when there is no such account.</summary>
    public async Task<PlayerSummary?> FindAsync(AccountId id, CancellationToken cancellationToken = default) =>
        (await ListAsync(cancellationToken)).FirstOrDefault(player => player.Account.Id == id);

    /// <summary>
    /// A page of the players whose name holds the search, without regard to case, ordered as asked; a page past the
    /// last is the last.
    /// </summary>
    public static PlayerPage Page(IReadOnlyList<PlayerSummary> players, PlayerSort sort, string? query, int page, int size = PerPage)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        var search = query?.Trim() ?? "";
        var matching = Order(players.Where(player => search.Length == 0 || player.Account.Username.Value.Contains(search, StringComparison.OrdinalIgnoreCase)), sort).ToList();
        var pages = Math.Max(1, (matching.Count + size - 1) / size);
        var shown = Math.Clamp(page, 1, pages);
        return new PlayerPage([.. matching.Skip((shown - 1) * size).Take(size)], players.Count, matching.Count, shown, pages);
    }

    private static PlayerSummary Summarize(Account account, AccountActivity? activity, IEnumerable<(AccountId Account, ResultSeat Seat, MatchResult Result)> played, Standing? standing)
    {
        // One account at both seats (a shared device) is one game, not two.
        var games = played.DistinctBy(entry => entry.Result.MatchId).ToList();
        return new PlayerSummary(
            account,
            activity,
            games.Count,
            games.Count(entry => entry.Result.Ranked),
            games.Count(entry => !entry.Result.AgainstBot),
            games.Count(entry => entry.Result.AgainstBot),
            games.Count(entry => Won(entry.Result, account.Id)),
            standing);
    }

    // A game an account held both seats of was won by it and lost by it alike, so it counts as neither.
    private static bool Won(MatchResult result, AccountId account) =>
        result.Winner is { } winner
        && result.Seats.Any(seat => seat.Seat == winner && seat.Account == account)
        && result.Seats.Any(seat => seat.Account != account);

    private static IEnumerable<PlayerSummary> Order(IEnumerable<PlayerSummary> players, PlayerSort sort)
    {
        var ordered = sort switch
        {
            PlayerSort.Name => players.OrderBy(player => player.Account.Username.Key, StringComparer.Ordinal),
            PlayerSort.Created => players.OrderByDescending(player => player.Account.CreatedAt),
            PlayerSort.SignedIn => players.OrderByDescending(player => player.Activity?.LastSignIn ?? DateTimeOffset.MinValue),
            PlayerSort.Games => players.OrderByDescending(player => player.Games),
            PlayerSort.Rating => players.OrderByDescending(player => player.Standing?.Rating.Rating ?? double.MinValue),
            _ => players.OrderByDescending(player => player.Activity?.LastSeen ?? DateTimeOffset.MinValue),
        };
        return ordered.ThenBy(player => player.Account.Username.Key, StringComparer.Ordinal);
    }
}
