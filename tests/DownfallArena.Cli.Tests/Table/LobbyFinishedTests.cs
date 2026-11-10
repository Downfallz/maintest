using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A player's games, as the lobby lists them (ADR 0147): every finished one from the kept results, a page at a
/// time, against bots apart from against players; and at most five of each kind in progress at once.
/// </summary>
public sealed class LobbyFinishedTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry = new();
    private readonly string _accountsRoot = Path.Combine(Path.GetTempPath(), $"lobby-finished-{Guid.CreateVersion7():N}");
    private readonly FileAccountStore _store;
    private readonly AccountRegistry _accounts;
    private readonly FileMatchResults _results;
    private AccountApi? _api;

    public LobbyFinishedTests()
    {
        _store = new FileAccountStore(_accountsRoot);
        _accounts = new AccountRegistry(_store, TimeProvider.System);
        _results = new FileMatchResults(_accountsRoot);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private async Task<LobbyApi> LobbyAsync()
    {
        _api ??= new AccountApi(_accounts, new AccountSessions(await _store.SessionKeyAsync(Cancel), TimeProvider.System), AccountsMode.Development);
        return new LobbyApi(_registry, _hosted.Composer, TimeProvider.System) { Accounts = _api, Results = _results };
    }

    private async Task<SignedIn> PlayerAsync(string name) =>
        new((await _accounts.RegisterAsync(new ExternalIdentity(ExternalIdentity.Development, name.ToLowerInvariant()), name, Cancel)).Value, null);

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private async Task<StudioResponse> OpenAsync(SignedIn player, string opponent) =>
        await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/tables", $$"""{"opponent":"{{opponent}}"}""", player: player);

    private Task RecordAsync(int minutes, ResultSeat you, ResultSeat other, string? winner = "player1", string? table = null) =>
        _results.RecordAsync(new MatchResult(Guid.CreateVersion7().ToString(), Start.AddMinutes(minutes), "3v3", "at-activation", "hash", 9, winner, [you, other], table), Cancel);

    [Fact]
    public async Task Finished_games_are_read_from_the_kept_results_newest_first_a_page_at_a_time_bots_apart_from_players()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        for (var game = 0; game < 8; game++)
        {
            await RecordAsync(game, new("player1", mark.Account.Id, null), new("player2", null, "greedy"));
        }

        await RecordAsync(20, new("player1", mark.Account.Id, null), new("player2", bastien.Account.Id, null), winner: "player2");
        await RecordAsync(21, new("player1", bastien.Account.Id, null), new("player2", null, "greedy"));
        var lobby = await LobbyAsync();

        var bots = Read(await lobby.HandleAsync("GET", "/api/lobby/finished/bots/1", "", player: mark));
        var later = Read(await lobby.HandleAsync("GET", "/api/lobby/finished/bots/2", "", player: mark));
        var players = Read(await lobby.HandleAsync("GET", "/api/lobby/finished/players/1", "", player: mark));

        bots.GetProperty("total").GetInt32().ShouldBe(8);
        bots.GetProperty("pages").GetInt32().ShouldBe(2);
        bots.GetProperty("items").GetArrayLength().ShouldBe(FinishedGames.PageSize);
        bots.GetProperty("items")[0].GetProperty("finishedAt").GetDateTimeOffset().ShouldBe(Start.AddMinutes(7));
        bots.GetProperty("items")[0].GetProperty("opponent").GetProperty("kind").GetString().ShouldBe("bot");
        bots.GetProperty("items")[0].GetProperty("outcome").GetString().ShouldBe("won");
        later.GetProperty("page").GetInt32().ShouldBe(2);
        later.GetProperty("items").GetArrayLength().ShouldBe(2);
        var against = players.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        against.GetProperty("opponent").GetProperty("name").GetString().ShouldBe("bastien");
        against.GetProperty("outcome").GetString().ShouldBe("lost");
    }

    [Fact]
    public async Task A_finished_game_the_host_no_longer_holds_opens_its_battle_replay_from_the_player_seat_and_one_kept_before_tables_were_named_opens_nothing()
    {
        var mark = await PlayerAsync("MarkF");
        await RecordAsync(0, new("player1", null, "greedy"), new("player2", mark.Account.Id, null), table: "swept-table");
        await RecordAsync(1, new("player1", mark.Account.Id, null), new("player2", null, "greedy"), winner: null);

        var items = Read(await (await LobbyAsync()).HandleAsync("GET", "/api/lobby/finished/bots/1", "", player: mark)).GetProperty("items");

        items[0].GetProperty("outcome").GetString().ShouldBe("draw");
        items[0].GetProperty("replay").ValueKind.ShouldBe(JsonValueKind.Null);
        items[1].GetProperty("replay").GetString().ShouldBe("/battle?replay=swept-table&seat=player2");
        items[1].GetProperty("seats").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>
    /// A finished game is watched again by the players who sat at it (ADR 0156): the lobby serves its replay to an
    /// account a kept result names at that table, asks the signed-out to sign in, and refuses everybody else.
    /// </summary>
    [Theory]
    [InlineData("MarkF", 200)]
    [InlineData("bastien", 403)]
    [InlineData(null, 401)]
    public async Task A_finished_game_replay_is_served_to_the_players_who_sat_at_it_only(string? who, int status)
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await RecordAsync(0, new("player1", mark.Account.Id, null), new("player2", null, "greedy"), table: "swept-table");
        var lobby = await LobbyAsync();
        lobby.Replay = id => Task.FromResult(StudioResponse.OfPlainText(200, $"replay of {id}"));
        var player = who switch { "MarkF" => mark, "bastien" => bastien, _ => null };

        var answer = await lobby.HandleAsync("GET", "/api/lobby/tables/swept-table/replay", "", player: player);

        answer.Status.ShouldBe(status);
    }

    [Fact]
    public async Task A_host_that_records_nothing_has_no_replay_to_serve_its_players()
    {
        var mark = await PlayerAsync("MarkF");
        await RecordAsync(0, new("player1", mark.Account.Id, null), new("player2", null, "greedy"), table: "swept-table");

        var answer = await (await LobbyAsync()).HandleAsync("GET", "/api/lobby/tables/swept-table/replay", "", player: mark);

        answer.Status.ShouldBe(404);
    }

    [Fact]
    public async Task Nobody_signed_in_reads_no_finished_games_and_a_page_that_is_not_one_is_no_route()
    {
        var lobby = await LobbyAsync();

        (await lobby.HandleAsync("GET", "/api/lobby/finished/bots/1", "")).Status.ShouldBe(401);
        (await lobby.HandleAsync("GET", "/api/lobby/finished/bots/0", "", player: await PlayerAsync("MarkF"))).Status.ShouldBe(404);
        (await lobby.HandleAsync("GET", "/api/lobby/finished/friends/1", "", player: await PlayerAsync("bastien"))).Status.ShouldBe(404);
    }

    [Fact]
    public async Task A_sixth_game_against_bots_is_refused_while_a_game_against_a_player_still_opens()
    {
        var mark = await PlayerAsync("MarkF");
        for (var game = 0; game < LobbyApi.OngoingPerKind; game++)
        {
            (await OpenAsync(mark, "bot")).Status.ShouldBe(201);
        }

        var sixth = await OpenAsync(mark, "bot");
        var person = await OpenAsync(mark, "person");

        sixth.Status.ShouldBe(409);
        Read(sixth).GetProperty("error").GetString().ShouldBe("Lobby.TooManyGames");
        person.Status.ShouldBe(201);
        Read(await (await LobbyAsync()).HandleAsync("GET", "/api/lobby/mine", "", player: mark)).GetProperty("ongoingPerKind").GetInt32().ShouldBe(LobbyApi.OngoingPerKind);
    }

    [Fact]
    public async Task A_sixth_game_against_players_is_refused_whether_opened_or_joined()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        for (var game = 0; game < LobbyApi.OngoingPerKind; game++)
        {
            (await OpenAsync(mark, "person")).Status.ShouldBe(201);
        }

        var theirs = Read(await OpenAsync(bastien, "person")).GetProperty("table").GetProperty("id").GetString();
        var opened = await OpenAsync(mark, "hotseat");
        var joined = await (await LobbyAsync()).HandleAsync("POST", $"/api/lobby/tables/{theirs}/join", "{}", player: mark);

        opened.Status.ShouldBe(409);
        joined.Status.ShouldBe(409);
        Read(joined).GetProperty("error").GetString().ShouldBe("Lobby.TooManyGames");
        _registry.ById(theirs!)!.Seats[1].Taken.ShouldBeFalse();
    }

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
        if (Directory.Exists(_accountsRoot))
        {
            Directory.Delete(_accountsRoot, recursive: true);
        }
    }
}
