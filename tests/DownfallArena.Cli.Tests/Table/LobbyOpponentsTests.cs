using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Friends;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The players a signed-in player met most recently, as the lobby lists them to challenge again: one entry each,
/// with the record between them, the last game's format and rating, and where their friendship stands (ADR 0153:
/// a friend is invited, anyone else is asked to be a friend first).
/// </summary>
public sealed class LobbyOpponentsTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lobby-opponents-{Guid.CreateVersion7():N}");
    private readonly FileAccountStore _store;
    private readonly AccountRegistry _accounts;
    private readonly FileMatchResults _results;
    private readonly Friends _friends;
    private AccountApi? _api;

    public LobbyOpponentsTests()
    {
        _store = new FileAccountStore(_root);
        _accounts = new AccountRegistry(_store, TimeProvider.System);
        _results = new FileMatchResults(_root);
        _friends = new Friends(new FileFriendStore(_root), _store, TimeProvider.System);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private async Task<LobbyApi> LobbyAsync(bool friends = true)
    {
        _api ??= new AccountApi(_accounts, new AccountSessions(await _store.SessionKeyAsync(Cancel), TimeProvider.System), AccountsMode.Development);
        return new LobbyApi(_registry, _hosted.Composer, TimeProvider.System) { Accounts = _api, Results = _results, Friends = friends ? _friends : null };
    }

    private async Task<SignedIn> PlayerAsync(string name) =>
        new((await _accounts.RegisterAsync(new ExternalIdentity(ExternalIdentity.Development, name.ToLowerInvariant()), name, Cancel)).Value, null);

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private Task RecordAsync(int minutes, SignedIn you, ResultSeat other, string? winner = "player1", string format = "3v3", bool casual = false) =>
        _results.RecordAsync(new MatchResult(Guid.CreateVersion7().ToString(), Start.AddMinutes(minutes), format, "at-activation", "hash", 9, winner,
            [new("player1", you.Account.Id, null), other], Casual: casual), Cancel);

    private async Task<JsonElement> OpponentsAsync(SignedIn player, bool friends = true) =>
        Read(await (await LobbyAsync(friends)).HandleAsync("GET", "/api/lobby/opponents", "", player: player)).GetProperty("opponents");

    [Fact]
    public async Task Each_player_met_is_listed_once_with_the_record_between_them_and_the_last_games_format_the_last_met_first()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        var julie = await PlayerAsync("julie");
        await RecordAsync(0, mark, new("player2", bastien.Account.Id, null), winner: "player2");
        await RecordAsync(1, mark, new("player2", null, "greedy"));
        await RecordAsync(2, mark, new("player2", julie.Account.Id, null));
        await RecordAsync(3, mark, new("player2", bastien.Account.Id, null), format: "1v1", casual: true);

        var opponents = await OpponentsAsync(mark);

        opponents.EnumerateArray().Select(opponent => opponent.GetProperty("name").GetString()).ShouldBe(["bastien", "julie"]);
        var first = opponents[0];
        first.GetProperty("id").GetString().ShouldBe(bastien.Account.Id.Value);
        first.GetProperty("games").GetInt32().ShouldBe(2);
        first.GetProperty("won").GetInt32().ShouldBe(1);
        first.GetProperty("lost").GetInt32().ShouldBe(1);
        first.GetProperty("format").GetString().ShouldBe("1v1");
        first.GetProperty("casual").GetBoolean().ShouldBeTrue();
        first.GetProperty("lastPlayedAt").GetDateTimeOffset().ShouldBe(Start.AddMinutes(3));
    }

    [Fact]
    public async Task Each_opponent_says_where_the_friendship_stands()
    {
        var mark = await PlayerAsync("MarkF");
        var friend = await PlayerAsync("friend");
        var asked = await PlayerAsync("asked");
        var asking = await PlayerAsync("asking");
        var stranger = await PlayerAsync("stranger");
        await _friends.AskAsync(mark.Account.Id, "friend", Cancel);
        await _friends.AskAsync(friend.Account.Id, "MarkF", Cancel);
        await _friends.AskAsync(mark.Account.Id, "asked", Cancel);
        await _friends.AskAsync(asking.Account.Id, "MarkF", Cancel);
        SignedIn[] met = [friend, asked, asking, stranger];
        for (var game = 0; game < met.Length; game++)
        {
            await RecordAsync(game, mark, new("player2", met[game].Account.Id, null));
        }

        var relations = (await OpponentsAsync(mark)).EnumerateArray().Select(opponent => (opponent.GetProperty("name").GetString(), opponent.GetProperty("relation").GetString()));

        relations.ShouldBe([("stranger", "none"), ("asking", "asking"), ("asked", "asked"), ("friend", "friend")]);
    }

    [Fact]
    public async Task An_account_deleted_since_is_nobody_to_challenge()
    {
        var mark = await PlayerAsync("MarkF");
        var gone = await PlayerAsync("gone");
        await RecordAsync(0, mark, new("player2", gone.Account.Id, null));

        await _accounts.DeleteAsync(gone.Account.Id, Cancel);

        (await OpponentsAsync(mark)).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_host_that_keeps_no_friends_lists_opponents_without_a_relation()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await RecordAsync(0, mark, new("player2", bastien.Account.Id, null));

        var opponent = (await OpponentsAsync(mark, friends: false)).EnumerateArray().ShouldHaveSingleItem();

        opponent.GetProperty("relation").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Nobody_signed_in_reads_no_opponents()
    {
        var lobby = await LobbyAsync();

        var response = await lobby.HandleAsync("GET", "/api/lobby/opponents", "");

        response.Status.ShouldBe(401);
    }

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
