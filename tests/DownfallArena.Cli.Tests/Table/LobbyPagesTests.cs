using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// Find an opponent, a page at a time: the lobby's poll carries how many games are in progress, never the games, and
/// the games are served eight at a time, newest first, those with a seat to take or all of them.
/// </summary>
public sealed class LobbyPagesTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);
    private readonly TableRegistry _registry = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lobby-pages-{Guid.CreateVersion7():N}");
    private readonly FileAccountStore _store;
    private readonly AccountRegistry _accounts;
    private AccountApi? _api;

    public LobbyPagesTests()
    {
        _store = new FileAccountStore(_root);
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

    private async Task<string> OpenAsync(SignedIn player, string opponent) =>
        Read(await (await LobbyAsync()).HandleAsync("POST", "/api/lobby/tables", $$"""{"opponent":"{{opponent}}"}""", player: player)).GetProperty("table").GetProperty("id").GetString()!;

    private async Task<JsonElement> PageAsync(string view, int page, SignedIn? player = null) =>
        Read(await (await LobbyAsync()).HandleAsync("GET", $"/api/lobby/page/{view}/{page}", "", player: player));

    /// <summary>Ten players who each opened a table for anyone, and one who plays a bot, in that order.</summary>
    private async Task<(string[] Open, string Bot)> TablesAsync()
    {
        var open = new string[10];
        for (var index = 0; index < open.Length; index++)
        {
            open[index] = await OpenAsync(await PlayerAsync($"rival{(char)('a' + index)}"), "person");
        }

        return (open, await OpenAsync(await PlayerAsync("botplayer"), "bot"));
    }

    [Fact]
    public async Task The_lobbys_poll_counts_the_games_in_progress_and_carries_none_of_them()
    {
        await TablesAsync();

        var listed = Read(await (await LobbyAsync()).HandleAsync("GET", "/api/lobby", ""));

        listed.TryGetProperty("tables", out _).ShouldBeFalse();
        listed.GetProperty("ongoing").GetInt32().ShouldBe(11);
        listed.GetProperty("open").GetInt32().ShouldBe(10);
    }

    [Fact]
    public async Task Open_seats_are_served_eight_at_a_time_newest_first_and_a_page_past_the_end_shows_the_last()
    {
        var (open, _) = await TablesAsync();

        var first = await PageAsync("open", 1);
        var second = await PageAsync("open", 2);
        var past = await PageAsync("open", 9);

        first.GetProperty("total").GetInt32().ShouldBe(10);
        first.GetProperty("pages").GetInt32().ShouldBe(2);
        first.GetProperty("items").GetArrayLength().ShouldBe(LobbyApi.PageSize);
        first.GetProperty("items")[0].GetProperty("id").GetString().ShouldBe(open[^1]);
        second.GetProperty("items").EnumerateArray().Select(table => table.GetProperty("id").GetString()).ShouldBe([open[1], open[0]]);
        past.GetProperty("page").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task All_games_include_the_ones_with_no_seat_to_take()
    {
        var (_, bot) = await TablesAsync();

        var all = await PageAsync("all", 1);

        all.GetProperty("total").GetInt32().ShouldBe(11);
        all.GetProperty("items")[0].GetProperty("id").GetString().ShouldBe(bot);
        (await PageAsync("open", 1)).GetProperty("items").EnumerateArray().ShouldNotContain(table => table.GetProperty("id").GetString() == bot);
    }

    [Fact]
    public async Task A_signed_in_players_own_open_table_is_not_offered_back_to_them()
    {
        var mark = await PlayerAsync("MarkF");
        var mine = await OpenAsync(mark, "person");
        var theirs = await OpenAsync(await PlayerAsync("bastien"), "person");

        var offered = (await PageAsync("open", 1, mark)).GetProperty("items").EnumerateArray().Select(table => table.GetProperty("id").GetString()).ToArray();
        var all = (await PageAsync("all", 1, mark)).GetProperty("items").EnumerateArray().Select(table => table.GetProperty("id").GetString()).ToArray();

        offered.ShouldBe([theirs]);
        all.ShouldBe([theirs, mine]);
    }

    [Fact]
    public async Task A_view_or_a_page_that_is_not_one_is_no_route()
    {
        var lobby = await LobbyAsync();

        (await lobby.HandleAsync("GET", "/api/lobby/page/mine/1", "")).Status.ShouldBe(404);
        (await lobby.HandleAsync("GET", "/api/lobby/page/open/0", "")).Status.ShouldBe(404);
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
