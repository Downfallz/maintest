using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Players;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The operator's players (ADR 0174): the list, an account's sheet, and the three things the operator does to an
/// account. Accounts are signed in by name (<c>--accounts dev</c>), so each test reads as players arriving.
/// </summary>
public sealed class PlayersApiTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"players-api-{Guid.CreateVersion7():N}");
    private readonly MovableClock _clock = new(Start);
    private readonly FileAccountStore _store;
    private readonly FileActivityStore _activity;
    private readonly FileMatchResults _results;

    public PlayersApiTests()
    {
        _store = new FileAccountStore(_root);
        _activity = new FileActivityStore(_root);
        _results = new FileMatchResults(Path.Combine(_root, "results"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private async Task<AccountApi> AccountsAsync()
    {
        return new AccountApi(new AccountRegistry(_store, _clock), new AccountSessions(await _store.SessionKeyAsync(Cancel), _clock), AccountsMode.Development)
        {
            Activity = new AccountActivityLog(_activity, _clock),
        };
    }

    private PlayersApi Players(AccountApi accounts) =>
        new(new PlayerDirectory(_store, accounts.Activity!, _results, _clock), accounts, _results);

    private static async Task<(AccountRequest Request, string Id)> SignInAsync(AccountApi accounts, string name)
    {
        var signedIn = await accounts.HandleAsync("POST", "/api/account/dev", $$"""{"username":"{{name}}"}""", new AccountRequest(null, Secure: false));
        var request = new AccountRequest(signedIn.Headers!.Single().Value.Split(';')[0][(AccountSessions.Cookie.Length + 1)..], Secure: false);
        return (request, Json(signedIn).GetProperty("account").GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task A_sign_in_and_a_later_visit_are_kept_as_the_accounts_activity()
    {
        var accounts = await AccountsAsync();
        var mark = await SignInAsync(accounts, "Mark");
        _clock.Advance(TimeSpan.FromHours(3));

        await accounts.WhoAsync(mark.Request, Cancel);

        (await _activity.FindAsync(AccountId.Parse(mark.Id), Cancel)).ShouldBe(new AccountActivity(AccountId.Parse(mark.Id), Start, Start.AddHours(3)));
    }

    [Fact]
    public async Task The_list_says_each_players_activity_and_games_newest_visit_first()
    {
        var accounts = await AccountsAsync();
        var mark = await SignInAsync(accounts, "Mark");
        _clock.Advance(TimeSpan.FromHours(2));
        var julie = await SignInAsync(accounts, "Julie");
        await _results.RecordAsync(new MatchResult(Guid.CreateVersion7().ToString(), Start, "3v3", "at-activation", "hash", 9, "player1", [new("player1", AccountId.Parse(mark.Id), null), new("player2", AccountId.Parse(julie.Id), null)]), Cancel);

        var page = Json(await Players(accounts).HandleAsync("GET", PlayersApi.Prefix, "", null, "op"));

        page.GetProperty("total").GetInt32().ShouldBe(2);
        var rows = page.GetProperty("players").EnumerateArray().ToArray();
        rows.Select(row => row.GetProperty("username").GetString()).ShouldBe(["Julie", "Mark"]);
        rows[1].GetProperty("games").GetInt32().ShouldBe(1);
        rows[1].GetProperty("wins").GetInt32().ShouldBe(1);
        rows[1].GetProperty("rating").GetInt32().ShouldBeGreaterThan(1500);
        rows[1].GetProperty("lastSignIn").GetDateTimeOffset().ShouldBe(Start);
    }

    [Fact]
    public async Task The_list_is_searched_sorted_and_paged_as_its_address_asks()
    {
        var accounts = await AccountsAsync();
        foreach (var name in new[] { "Mark", "Marcel", "Julie" })
        {
            await SignInAsync(accounts, name);
        }

        var page = Json(await Players(accounts).HandleAsync("GET", PlayersApi.Prefix, "", "?q=mar&sort=name&page=4", "op"));

        page.GetProperty("players").EnumerateArray().Select(row => row.GetProperty("username").GetString()).ShouldBe(["Marcel", "Mark"]);
        page.GetProperty("matching").GetInt32().ShouldBe(2);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("sort").GetString().ShouldBe("name");
    }

    [Fact]
    public async Task An_accounts_sheet_lists_its_last_games_against_whom()
    {
        var accounts = await AccountsAsync();
        var mark = await SignInAsync(accounts, "Mark");
        await _results.RecordAsync(new MatchResult(Guid.CreateVersion7().ToString(), Start, "1v1", "at-activation", "hash", 7, "player2", [new("player1", AccountId.Parse(mark.Id), null), new("player2", null, "greedy")]), Cancel);

        var sheet = Json(await Players(accounts).HandleAsync("GET", $"{PlayersApi.Prefix}/{mark.Id}", "", null, "op"));

        sheet.GetProperty("player").GetProperty("username").GetString().ShouldBe("Mark");
        var game = sheet.GetProperty("recent").EnumerateArray().ShouldHaveSingleItem();
        game.GetProperty("outcome").GetString().ShouldBe("loss");
        game.GetProperty("format").GetString().ShouldBe("1v1");
        game.GetProperty("opponent").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task An_account_nobody_has_is_not_found()
    {
        var accounts = await AccountsAsync();

        (await Players(accounts).HandleAsync("GET", $"{PlayersApi.Prefix}/{AccountId.New().Value}", "", null, "op")).Status.ShouldBe(404);
        (await Players(accounts).HandleAsync("GET", $"{PlayersApi.Prefix}/not-an-id", "", null, "op")).Status.ShouldBe(404);
    }

    [Fact]
    public async Task Signing_an_account_out_everywhere_ends_every_session_it_holds()
    {
        var accounts = await AccountsAsync();
        var mark = await SignInAsync(accounts, "Mark");

        var answer = await Players(accounts).HandleAsync("POST", $"{PlayersApi.Prefix}/{mark.Id}/sign-out", "", null, "op");

        answer.Status.ShouldBe(200);
        (await accounts.WhoAsync(mark.Request, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_renamed_account_keeps_its_sessions_and_answers_to_its_new_name()
    {
        var accounts = await AccountsAsync();
        var mark = await SignInAsync(accounts, "Mark");

        var answer = await Players(accounts).HandleAsync("POST", $"{PlayersApi.Prefix}/{mark.Id}/rename", """{"username":"Marko"}""", null, "op");

        answer.Status.ShouldBe(200);
        (await accounts.WhoAsync(mark.Request, Cancel)).ShouldNotBeNull().Account.Username.Value.ShouldBe("Marko");
    }

    [Fact]
    public async Task A_rename_to_a_name_another_player_holds_is_refused()
    {
        var accounts = await AccountsAsync();
        var mark = await SignInAsync(accounts, "Mark");
        await SignInAsync(accounts, "Julie");

        var answer = await Players(accounts).HandleAsync("POST", $"{PlayersApi.Prefix}/{mark.Id}/rename", """{"username":"julie"}""", null, "op");

        answer.Status.ShouldBe(409);
        Json(answer).GetProperty("error").GetString().ShouldBe(AccountErrors.UsernameTaken.Code);
    }

    [Fact]
    public async Task Deleting_an_account_needs_its_name_typed()
    {
        var accounts = await AccountsAsync();
        var mark = await SignInAsync(accounts, "Mark");

        var answer = await Players(accounts).HandleAsync("DELETE", $"{PlayersApi.Prefix}/{mark.Id}", """{"username":"Someone"}""", null, "op");

        answer.Status.ShouldBe(400);
        (await _store.FindAsync(AccountId.Parse(mark.Id), Cancel)).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_deleted_account_is_gone_with_its_activity_and_its_sessions()
    {
        var accounts = await AccountsAsync();
        var mark = await SignInAsync(accounts, "Mark");

        var answer = await Players(accounts).HandleAsync("DELETE", $"{PlayersApi.Prefix}/{mark.Id}", """{"username":"mark"}""", null, "op");

        answer.Status.ShouldBe(200);
        (await _store.FindAsync(AccountId.Parse(mark.Id), Cancel)).ShouldBeNull();
        (await _activity.FindAsync(AccountId.Parse(mark.Id), Cancel)).ShouldBeNull();
        (await accounts.WhoAsync(mark.Request, Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task A_player_deleting_their_own_account_forgets_its_activity_too()
    {
        var accounts = await AccountsAsync();
        var mark = await SignInAsync(accounts, "Mark");

        await accounts.HandleAsync("DELETE", AccountApi.Prefix, """{"username":"Mark"}""", mark.Request);

        (await _activity.FindAsync(AccountId.Parse(mark.Id), Cancel)).ShouldBeNull();
    }

    [Fact]
    public void An_unknown_sort_is_the_last_visit()
    {
        PlayersApi.SortOf("whatever").ShouldBe(PlayerSort.Seen);
        PlayersApi.SortOf("Games").ShouldBe(PlayerSort.Games);
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
