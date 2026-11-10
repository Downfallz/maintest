using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>A host where players sign in (ADR 0122, stage 3): playing needs an account, and a seat is its account's.</summary>
public sealed class LobbyAccountsTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry = new();
    private readonly string _accountsRoot = Path.Combine(Path.GetTempPath(), $"lobby-accounts-{Guid.CreateVersion7():N}");
    private readonly FileAccountStore _store;
    private readonly AccountRegistry _accounts;
    private AccountApi? _api;

    public LobbyAccountsTests()
    {
        _store = new FileAccountStore(_accountsRoot);
        _accounts = new AccountRegistry(_store, TimeProvider.System);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<LobbyApi> LobbyAsync()
    {
        _api ??= new AccountApi(_accounts, new AccountSessions(await _store.SessionKeyAsync(Cancel), TimeProvider.System), AccountsMode.Development);
        return new LobbyApi(_registry, _hosted.Composer, TimeProvider.System) { Accounts = _api };
    }

    private async Task<SignedIn> PlayerAsync(string name) =>
        new((await _accounts.RegisterAsync(new ExternalIdentity(ExternalIdentity.Development, name.ToLowerInvariant()), name, Cancel)).Value, null);

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private async Task<(PlayedTable Table, JsonElement Opened)> OpenAsync(SignedIn player, string opponent = "person")
    {
        var opened = Read(await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/tables", $$"""{"opponent":"{{opponent}}"}""", player: player));
        return (_registry.ById(opened.GetProperty("table").GetProperty("id").GetString()!)!, opened);
    }

    [Fact]
    public async Task Nobody_signed_in_opens_a_table_takes_a_place_or_accepts_an_invitation()
    {
        var (table, _) = await OpenAsync(await PlayerAsync("MarkF"));
        var lobby = await LobbyAsync();

        var opened = await lobby.HandleAsync("POST", "/api/lobby/tables", "{}");
        var joined = await lobby.HandleAsync("POST", $"/api/lobby/tables/{table.Id}/join", "{}");
        var claimed = await lobby.HandleAsync("POST", "/api/lobby/claim", JsonSerializer.Serialize(new { tokens = new[] { table.Seats[1].Invitation } }));

        new[] { opened.Status, joined.Status, claimed.Status }.ShouldAllBe(status => status == 401);
        Read(opened).GetProperty("error").GetString().ShouldBe("Account.SignInRequired");
        table.Seats[1].Taken.ShouldBeFalse();
    }

    /// <summary>A saved seat signed out is a player to send to sign in, not a seat to forget.</summary>
    [Fact]
    public async Task Resuming_saved_seats_signed_out_asks_for_a_sign_in_instead_of_answering_none()
    {
        var mark = await PlayerAsync("MarkF");
        var (table, _) = await OpenAsync(mark);
        var lobby = await LobbyAsync();
        var tokens = JsonSerializer.Serialize(new { tokens = new[] { table.Seats[0].Token } });

        var signedOut = await lobby.HandleAsync("POST", "/api/lobby/resume", tokens);
        var signedIn = Read(await lobby.HandleAsync("POST", "/api/lobby/resume", tokens, player: mark));

        signedOut.Status.ShouldBe(401);
        Read(signedOut).GetProperty("error").GetString().ShouldBe("Account.SignInRequired");
        signedIn.GetProperty("tables").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task A_seat_handed_out_belongs_to_the_account_that_took_it_and_says_so_after_a_redeploy()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        var (table, _) = await OpenAsync(mark);
        (await (await LobbyAsync()).HandleAsync("POST", $"/api/lobby/tables/{table.Id}/join", "{}", player: bastien)).Status.ShouldBe(200);

        table.Seats[0].Account.ShouldBe(mark.Account.Id);
        table.Seats[1].Account.ShouldBe(bastien.Account.Id);
        using var after = new HostedTables(_ => new FileArtifactStore(_hosted.RunsDirectory));
        using var restored = new TableRegistry();
        await TableRestorer.RestoreAsync(after.Stored!, after.Composer, restored, TimeProvider.System, Cancel);
        var rebuilt = restored.ById(table.Id).ShouldNotBeNull();
        rebuilt.Seats.Select(seat => seat.Account).ShouldBe([mark.Account.Id, bastien.Account.Id]);
        restored.Dispose();
        await rebuilt.QuiesceAsync(Cancel);
    }

    [Fact]
    public async Task A_card_says_who_plays_whom_by_their_names()
    {
        var mark = await PlayerAsync("MarkF");
        await OpenAsync(mark);
        await OpenAsync(mark, opponent: "bot");

        var tables = Read(await (await LobbyAsync()).HandleAsync("GET", "/api/lobby/page/all/1", "")).GetProperty("items").EnumerateArray()
            .Select(table => table.GetProperty("seats").EnumerateArray().Select(seat => $"{seat.GetProperty("kind").GetString()}:{seat.GetProperty("name").GetString()}").ToArray())
            .ToList();

        tables.ShouldContain(seats => seats.SequenceEqual(new[] { "person:MarkF", "open:Open seat" }));
        tables.ShouldContain(seats => seats[0] == "person:MarkF" && seats[1].StartsWith("bot:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_card_says_whose_turn_it_is_once_the_match_asks_someone()
    {
        var (table, _) = await OpenAsync(await PlayerAsync("MarkF"), opponent: "bot");
        var lobby = await LobbyAsync();
        static bool[] Turns(JsonElement listed) => [.. listed.GetProperty("items")[0].GetProperty("seats").EnumerateArray().Select(seat => seat.GetProperty("turn").GetBoolean())];
        Turns(Read(await lobby.HandleAsync("GET", "/api/lobby/page/all/1", ""))).ShouldBe([false, false]);

        table.Seats[0].Person!.Arrive();
        table.Session.Begin();
        for (var attempt = 0; attempt < 250 && table.Seats[0].Person!.Waiting is null; attempt++)
        {
            await Task.Delay(20, Cancel);
        }

        Turns(Read(await lobby.HandleAsync("GET", "/api/lobby/page/all/1", ""))).ShouldBe([true, false]);
    }

    [Fact]
    public async Task Your_games_are_the_hosts_list_of_the_seats_your_account_holds()
    {
        var mark = await PlayerAsync("MarkF");
        var (mine, _) = await OpenAsync(mark);
        await OpenAsync(await PlayerAsync("bastien"));

        var listed = Read(await (await LobbyAsync()).HandleAsync("GET", "/api/lobby/mine", "", player: mark)).GetProperty("tables");

        listed.GetArrayLength().ShouldBe(1);
        listed[0].GetProperty("table").GetProperty("id").GetString().ShouldBe(mine.Id);
        listed[0].GetProperty("seats")[0].GetProperty("token").GetString().ShouldBe(mine.Seats[0].Token);
    }

    [Fact]
    public async Task A_player_does_not_take_the_other_place_of_a_table_they_already_play()
    {
        var mark = await PlayerAsync("MarkF");
        var (table, _) = await OpenAsync(mark);

        var joined = await (await LobbyAsync()).HandleAsync("POST", $"/api/lobby/tables/{table.Id}/join", "{}", player: mark);

        joined.Status.ShouldBe(409);
        table.Seats[1].Taken.ShouldBeFalse();
    }

    [Fact]
    public async Task Another_account_cannot_leave_a_seat_with_its_token_and_its_own_player_can()
    {
        var mark = await PlayerAsync("MarkF");
        var (table, _) = await OpenAsync(mark);
        var token = table.Seats[0].Token;
        var lobby = await LobbyAsync();

        (await lobby.HandleAsync("POST", "/api/lobby/leave", "", token, await PlayerAsync("bastien"))).Status.ShouldBe(403);
        table.Seats[0].Account.ShouldBe(mark.Account.Id);

        (await lobby.HandleAsync("POST", "/api/lobby/leave", "", token, mark)).Status.ShouldBe(200);
        table.Seats[0].Account.ShouldBeNull();
    }

    [Fact]
    public async Task Seats_a_browser_saved_before_signing_in_become_the_accounts_unless_another_holds_them()
    {
        var lobby = new LobbyApi(_registry, _hosted.Composer, TimeProvider.System);
        var saved = Read(await lobby.HandleAsync("POST", "/api/lobby/tables", "{}")).GetProperty("seats")[0].GetProperty("token").GetString()!;
        var bastien = await PlayerAsync("bastien");
        var (theirs, _) = await OpenAsync(bastien);
        var mark = await PlayerAsync("MarkF");

        var adopted = Read(await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/adopt", JsonSerializer.Serialize(new { tokens = new[] { saved, theirs.Seats[0].Token } }), player: mark));

        adopted.GetProperty("tables").GetArrayLength().ShouldBe(1);
        _registry.ByToken(saved)!.Seats[0].Account.ShouldBe(mark.Account.Id);
        theirs.Seats[0].Account.ShouldBe(bastien.Account.Id);
    }

    [Fact]
    public async Task A_deleted_accounts_seats_in_unfinished_games_are_offered_to_other_players()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        var (marks, _) = await OpenAsync(mark);
        var (theirs, _) = await OpenAsync(bastien);
        var token = marks.Seats[0].Token;
        var lobby = await LobbyAsync();

        var offered = await lobby.ReleaseAsync(mark.Account.Id);

        offered.ShouldBe(1);
        marks.Seats[0].Account.ShouldBeNull();
        marks.OpenSeats.ShouldContain("player1");
        _registry.ByToken(token).ShouldBeNull();
        theirs.Seats[0].Account.ShouldBe(bastien.Account.Id);
        (await _hosted.Stored!.RecordAsync(marks.Id, Cancel)).ShouldNotBeNull().Seats.Single(seat => seat.Slot == "player1").Account.ShouldBeNull();
    }

    [Theory]
    [InlineData("Lookahead (lookahead-55) — the strongest: searched from search-51's weights", "Lookahead (lookahead-55)")]
    [InlineData("Greedy", "Greedy")]
    [InlineData("heuristic:learning/weights/search-19.json", "heuristic")]
    [InlineData("odd/name", "odd/name")]
    public void A_bot_is_named_on_a_card_by_its_label_without_the_description_or_the_path(string label, string name)
    {
        LobbyApi.BotName(label).ShouldBe(name);
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
