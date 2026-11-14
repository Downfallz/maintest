using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Application.Accounts.Players;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Studio;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The operator's players (headquarters A2, ADR 0174): every account with its activity, its games and its rating,
/// twenty a page, and one account's sheet with the three things the operator can do to it -- sign it out everywhere,
/// rename it, delete it. Reached through <see cref="AdminApi" />, behind its gate; never by a player.
/// </summary>
internal sealed class PlayersApi
{
    public const string Prefix = "/api/admin/players";

    /// <summary>How many of an account's finished games its sheet shows, the last first.</summary>
    public const int RecentGames = 8;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly PlayerDirectory _directory;
    private readonly AccountApi _accounts;
    private readonly IMatchResults? _results;

    public PlayersApi(PlayerDirectory directory, AccountApi accounts, IMatchResults? results = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(accounts);
        _directory = directory;
        _accounts = accounts;
        _results = results;
    }

    /// <summary>Friendships, for the count a sheet shows; none on a host that keeps none.</summary>
    public Friends? Friends { get; set; }

    /// <summary>How a bot is named, by its spec, as the lobby names it; the spec itself when none is set.</summary>
    public Func<string, string>? BotNamed { get; set; }

    public static bool Names(string path) => path == Prefix || path.StartsWith(Prefix + "/", StringComparison.Ordinal);

    public async Task<StudioResponse> HandleAsync(string method, string path, string body, string? query, string operatorName)
    {
        ArgumentNullException.ThrowIfNull(path);
        var rest = path[Prefix.Length..].Trim('/');
        var parts = rest.Split('/');
        try
        {
            return (method, parts) switch
            {
                ("GET", [""]) => await PageAsync(query),
                ("GET", [var id]) => await WithAccountAsync(id, SheetAsync),
                ("POST", [var id, "sign-out"]) => await WithAccountAsync(id, account => SignOutAsync(account, operatorName)),
                ("POST", [var id, "rename"]) => await WithAccountAsync(id, account => RenameAsync(account, body, operatorName)),
                ("DELETE", [var id]) => await WithAccountAsync(id, account => DeleteAsync(account, body, operatorName)),
                ("POST", [var id, "suspension"]) => await WithAccountAsync(id, account => SuspendAsync(account, body, operatorName)),
                ("DELETE", [var id, "suspension"]) => await WithAccountAsync(id, account => RestoreAsync(account, operatorName)),
                _ => Problem(404, "Players.NoRoute", $"No such route: {method} {path}"),
            };
        }
        catch (JsonException)
        {
            return Problem(400, "Players.BadRequest", "The request is not valid JSON.");
        }
    }

    /// <summary>The words a page sorts by, and what each one means.</summary>
    public static PlayerSort SortOf(string? asked) => asked?.Trim().ToLowerInvariant() switch
    {
        "name" => PlayerSort.Name,
        "created" => PlayerSort.Created,
        "signed-in" => PlayerSort.SignedIn,
        "games" => PlayerSort.Games,
        "rating" => PlayerSort.Rating,
        _ => PlayerSort.Seen,
    };

    private static string Named(PlayerSort sort) => sort switch
    {
        PlayerSort.Name => "name",
        PlayerSort.Created => "created",
        PlayerSort.SignedIn => "signed-in",
        PlayerSort.Games => "games",
        PlayerSort.Rating => "rating",
        _ => "seen",
    };

    private async Task<StudioResponse> PageAsync(string? query)
    {
        var asked = System.Web.HttpUtility.ParseQueryString(query ?? string.Empty);
        var sort = SortOf(asked["sort"]);
        var page = int.TryParse(asked["page"], out var number) ? number : 1;
        var shown = PlayerDirectory.Page(await _directory.ListAsync(), sort, asked["q"], page);
        return Answer(new
        {
            players = shown.Players.Select(Row).ToArray(),
            total = shown.Total,
            matching = shown.Matching,
            page = shown.Page,
            pages = shown.Pages,
            perPage = PlayerDirectory.PerPage,
            sort = Named(sort),
            q = asked["q"]?.Trim() ?? "",
        });
    }

    private async Task<StudioResponse> SheetAsync(Account account)
    {
        if (await _directory.FindAsync(account.Id) is not { } player)
        {
            return Gone();
        }

        var devices = _accounts.Devices is { } held ? (await held.ListAsync(account.Id)).Count : 0;
        var friends = Friends is { } kept ? await kept.ListAsync(account.Id) : FriendList.Empty;
        var results = _results is null ? [] : await _results.ListForAsync(account.Id);
        var recent = new List<object>();
        foreach (var result in results.OrderByDescending(result => result.FinishedAt).Take(RecentGames))
        {
            recent.Add(await GameAsync(result, account.Id));
        }

        return Answer(new
        {
            player = Row(player),
            devices,
            friends = friends.Friends.Count,
            requests = friends.Incoming.Count + friends.Outgoing.Count,
            recent,
        });
    }

    private async Task<object> GameAsync(MatchResult result, AccountId account)
    {
        var mine = result.Seats.FirstOrDefault(seat => seat.Account == account);
        var other = result.Seats.FirstOrDefault(seat => seat != mine);
        var opponent = other switch
        {
            { Agent: { } agent } => BotNamed?.Invoke(agent) ?? LobbyApi.BotName(agent),
            { Account: { } id } when id == account => "themselves",
            { Account: { } id } => (await _accounts.AccountAsync(id))?.Username.Value ?? "a deleted account",
            _ => "a guest",
        };
        var outcome = Outcome(result.Winner, mine, other, account);
        return new
        {
            finishedAt = result.FinishedAt,
            result.Format,
            result.Rounds,
            ranked = result.Ranked,
            opponent,
            outcome,
            table = result.Table,
        };
    }

    // A game one account held both seats of was neither won nor lost by it.
    private static string Outcome(string? winner, ResultSeat? mine, ResultSeat? other, AccountId account)
    {
        if (other?.Account == account)
        {
            return "played";
        }

        if (winner is null)
        {
            return "draw";
        }

        return winner == mine?.Seat ? "win" : "loss";
    }

    private async Task<StudioResponse> SignOutAsync(Account account, string operatorName)
    {
        if (await _accounts.SignOutEverywhereAsync(account.Id) is null)
        {
            return Gone();
        }

        Console.WriteLine($"  Account {account.Id.Value} ({account.Username.Value}) signed out everywhere by {operatorName}.");
        return Answer(new { signedOut = true });
    }

    /// <summary>Suspends the account with the reason its player will read (ADR 0178).</summary>
    private async Task<StudioResponse> SuspendAsync(Account account, string body, string operatorName)
    {
        var suspension = AccountSuspension.Create(Read<SuspensionBody>(body)?.Reason, operatorName, _accounts.Clock.GetUtcNow());
        if (suspension.IsFailure)
        {
            return Problem(400, suspension.Error.Code, suspension.Error.Message);
        }

        var suspended = await _accounts.SuspendAsync(account.Id, suspension.Value);
        if (suspended.IsFailure)
        {
            return Refused(suspended.Error);
        }

        Console.WriteLine($"  Account {account.Id.Value} ({account.Username.Value}) suspended by {operatorName}: {suspension.Value.Reason}");
        return Answer(new { suspension = Describe(suspended.Value.Suspension) });
    }

    private async Task<StudioResponse> RestoreAsync(Account account, string operatorName)
    {
        var restored = await _accounts.RestoreAsync(account.Id);
        if (restored.IsFailure)
        {
            return Refused(restored.Error);
        }

        Console.WriteLine($"  Account {account.Id.Value} ({account.Username.Value}) restored by {operatorName}.");
        return Answer(new { restored = true });
    }

    private static object? Describe(AccountSuspension? suspension) => suspension is null
        ? null
        : new { suspension.Reason, suspension.By, suspension.At };

    private async Task<StudioResponse> RenameAsync(Account account, string body, string operatorName)
    {
        var renamed = await _accounts.RenameAsync(account.Id, Read<NameBody>(body)?.Username);
        if (renamed.IsFailure)
        {
            return Refused(renamed.Error);
        }

        Console.WriteLine($"  Account {account.Id.Value} renamed from {account.Username.Value} to {renamed.Value.Username.Value} by {operatorName}.");
        return Answer(new { account = AccountApi.Describe(renamed.Value) });
    }

    /// <summary>Deleted as its player would delete it, once the operator has typed its name to confirm.</summary>
    private async Task<StudioResponse> DeleteAsync(Account account, string body, string operatorName)
    {
        if (!string.Equals(Read<NameBody>(body)?.Username?.Trim(), account.Username.Value, StringComparison.OrdinalIgnoreCase))
        {
            return Problem(400, "Players.ConfirmDelete", $"Type the player's name, {account.Username.Value}, to delete the account.");
        }

        if (!await _accounts.ForgetAsync(account.Id))
        {
            return Gone();
        }

        Console.WriteLine($"  Account {account.Id.Value} ({account.Username.Value}) deleted by {operatorName}.");
        return Answer(new { deleted = true });
    }

    private async Task<StudioResponse> WithAccountAsync(string id, Func<Account, Task<StudioResponse>> act)
    {
        if (!AccountId.TryParse(Uri.UnescapeDataString(id), out var parsed))
        {
            return Gone();
        }

        // Read from the store rather than the cache: the operator acts on the account as it is now.
        return await _accounts.FreshAsync(parsed) is { } account ? await act(account) : Gone();
    }

    private static object Row(PlayerSummary player) => new
    {
        id = player.Account.Id.Value,
        username = player.Account.Username.Value,
        createdAt = player.Account.CreatedAt,
        lastSignIn = player.Activity?.LastSignIn,
        lastSeen = player.Activity?.LastSeen,
        games = player.Games,
        ranked = player.Ranked,
        againstPlayers = player.AgainstPlayers,
        againstBots = player.AgainstBots,
        wins = player.Wins,
        rating = player.Standing is { } standing ? (int?)Math.Round(standing.Rating.Rating) : null,
        provisional = player.Standing?.Provisional,
        suspension = Describe(player.Account.Suspension),
    };

    private static StudioResponse Gone() => Problem(404, AccountErrors.NotFound.Code, AccountErrors.NotFound.Message);

    private static StudioResponse Refused(DomainError error)
    {
        var status = 400;
        if (error == AccountErrors.UsernameTaken || error == AccountErrors.AlreadySuspended || error == AccountErrors.NotSuspended)
        {
            status = 409;
        }
        else if (error == AccountErrors.NotFound)
        {
            status = 404;
        }

        return Problem(status, error.Code, error.Message);
    }

    private sealed record SuspensionBody(string? Reason);

    private static T? Read<T>(string body)
        where T : class => string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<T>(body, Web);

    private static StudioResponse Answer(object payload) => StudioResponse.OfJson(payload, Web);

    private static StudioResponse Problem(int status, string code, string message) =>
        StudioResponse.OfJson(new { error = code, message }, Web, status);

    private sealed record NameBody(string? Username);
}
