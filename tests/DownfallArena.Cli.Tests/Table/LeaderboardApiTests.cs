using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

public sealed class LeaderboardApiTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 20, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ladder-{Guid.CreateVersion7():N}");
    private readonly FileAccountStore _store;
    private readonly FileMatchResults _results;
    private readonly AccountRegistry _registry;

    public LeaderboardApiTests()
    {
        _store = new FileAccountStore(_root);
        _results = new FileMatchResults(_root);
        _registry = new AccountRegistry(_store, TimeProvider.System);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<AccountApi> AccountsAsync() =>
        new(_registry, new AccountSessions(await _store.SessionKeyAsync(Cancel), TimeProvider.System), AccountsMode.Development);

    private async Task<Account> PlayerAsync(string name) =>
        (await _registry.RegisterAsync(new ExternalIdentity(ExternalIdentity.Development, name.ToLowerInvariant()), name, Cancel)).Value;

    private Task RecordAsync(AccountId? first, AccountId? second, string? winner, int minutes, string? bot = null) => _results.RecordAsync(
        new MatchResult(Guid.CreateVersion7().ToString(), Start.AddMinutes(minutes), "3v3", "at-activation", "hash", 10, winner, [new("player1", first, null), new("player2", second, second is null ? bot : null)]),
        Cancel);

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private static async Task<string> SessionOfAsync(AccountApi accounts, string name)
    {
        var signedIn = await accounts.HandleAsync("POST", "/api/account/dev", JsonSerializer.Serialize(new { username = name }), new AccountRequest(null, Secure: false));
        return signedIn.Headers!.Single().Value.Split(';')[0][(AccountSessions.Cookie.Length + 1)..];
    }

    [Fact]
    public async Task The_ladder_names_each_ranked_player_by_their_name_in_order_of_rating()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await RecordAsync(mark.Id, bastien.Id, "player1", 0);
        await RecordAsync(mark.Id, bastien.Id, "player1", 1);
        var ladder = new LeaderboardApi(_results, await AccountsAsync(), new Clock(Start.AddMinutes(2)));

        var read = Read(await ladder.HandleAsync("GET", new AccountRequest(null, Secure: false)));

        var players = read.GetProperty("players").EnumerateArray().ToList();
        players.Select(player => player.GetProperty("username").GetString()).ShouldBe(["MarkF", "bastien"]);
        players[0].GetProperty("rank").GetInt32().ShouldBe(1);
        players[0].GetProperty("wins").GetInt32().ShouldBe(2);
        players[0].GetProperty("rating").GetInt32().ShouldBeGreaterThan(1500);
        players[0].GetProperty("provisional").GetBoolean().ShouldBeTrue();
        read.GetProperty("you").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_signed_in_player_is_shown_their_place_and_their_record_against_each_bot_by_its_name()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await RecordAsync(mark.Id, bastien.Id, "player2", 0);
        await RecordAsync(mark.Id, null, "player1", 1, bot: "greedy");
        var accounts = await AccountsAsync();
        var ladder = new LeaderboardApi(_results, accounts, new Clock(Start.AddMinutes(2)), spec => spec == "greedy" ? "Greedy" : spec);

        var you = Read(await ladder.HandleAsync("GET", new AccountRequest(await SessionOfAsync(accounts, "MarkF"), Secure: false))).GetProperty("you");

        you.GetProperty("username").GetString().ShouldBe("MarkF");
        you.GetProperty("standing").GetProperty("rank").GetInt32().ShouldBe(2);
        you.GetProperty("standing").GetProperty("you").GetBoolean().ShouldBeTrue();
        var bot = you.GetProperty("bots").EnumerateArray().ShouldHaveSingleItem();
        bot.GetProperty("bot").GetString().ShouldBe("Greedy");
        bot.GetProperty("wins").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task A_day_old_session_is_issued_again_by_the_ladder()
    {
        var clock = new MovableClock(Start);
        var accounts = new AccountApi(_registry, new AccountSessions(await _store.SessionKeyAsync(Cancel), clock), AccountsMode.Development);
        var session = await SessionOfAsync(accounts, "MarkF");
        var ladder = new LeaderboardApi(_results, accounts, clock);
        (await ladder.HandleAsync("GET", new AccountRequest(session, Secure: false))).Headers.ShouldBeNull();

        clock.Advance(AccountSessions.RenewAfter);
        var renewed = await ladder.HandleAsync("GET", new AccountRequest(session, Secure: false));

        renewed.Headers.ShouldNotBeNull().Single().Value.ShouldStartWith(AccountSessions.Cookie + "=");
    }

    [Fact]
    public async Task A_deleted_account_leaves_the_ladder()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("bastien");
        await RecordAsync(mark.Id, bastien.Id, "player1", 0);
        await _registry.DeleteAsync(bastien.Id, Cancel);
        var ladder = new LeaderboardApi(_results, await AccountsAsync(), new Clock(Start.AddMinutes(1)));

        var players = Read(await ladder.HandleAsync("GET", new AccountRequest(null, Secure: false))).GetProperty("players");

        players.EnumerateArray().Select(player => player.GetProperty("username").GetString()).ShouldBe(["MarkF"]);
    }

    [Fact]
    public async Task The_ladder_is_read_and_never_written()
    {
        var ladder = new LeaderboardApi(_results, await AccountsAsync(), TimeProvider.System);

        (await ladder.HandleAsync("POST", new AccountRequest(null, Secure: false))).Status.ShouldBe(405);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
