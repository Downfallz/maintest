using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>A signed-in player's own statistics (ADR 0168), served off their ranked results (ADR 0171) and named from the catalogue.</summary>
public sealed class StatsApiTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 20, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"stats-{Guid.CreateVersion7():N}");
    private readonly HostedTables _hosted = new();
    private readonly FileAccountStore _store;
    private readonly FileMatchResults _results;
    private readonly AccountRegistry _registry;

    public StatsApiTests()
    {
        _store = new FileAccountStore(_root);
        _results = new FileMatchResults(_root);
        _registry = new AccountRegistry(_store, TimeProvider.System);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private IGameResources Resources => _hosted.Services.GetRequiredService<IGameResources>();

    public void Dispose()
    {
        _hosted.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<AccountApi> AccountsAsync() =>
        new(_registry, new AccountSessions(await _store.SessionKeyAsync(Cancel), TimeProvider.System), AccountsMode.Development);

    private async Task<Account> PlayerAsync(string name) =>
        (await _registry.RegisterAsync(new ExternalIdentity(ExternalIdentity.Development, name.ToLowerInvariant()), name, Cancel)).Value;

    private static async Task<string> SessionOfAsync(AccountApi accounts, string name)
    {
        var signedIn = await accounts.HandleAsync("POST", "/api/account/dev", JsonSerializer.Serialize(new { username = name }), new AccountRequest(null, Secure: false));
        return signedIn.Headers!.Single().Value.Split(';')[0][(AccountSessions.Cookie.Length + 1)..];
    }

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    [Fact]
    public async Task A_signed_in_player_reads_their_own_ranked_record_and_what_they_played_by_name()
    {
        var mark = await PlayerAsync("MarkF");
        var bastien = await PlayerAsync("Bastien");
        var creature = Resources.Creatures.First();
        var spell = Resources.Spells.First();
        await _results.RecordAsync(
            new MatchResult(Guid.CreateVersion7().ToString(), Start, "3v3", "at-activation", "hash", 10, "player1", [new("player1", mark.Id, null), new("player2", bastien.Id, null)])
            {
                Teams = [new PlayedCreature("player1", creature.Id.Value, ["tier:gone:v1"], Survived: true, new Dictionary<string, int> { [spell.Id.Value] = 4 })],
            },
            Cancel);
        await _results.RecordAsync(
            new MatchResult(Guid.CreateVersion7().ToString(), Start.AddHours(1), "3v3", "at-activation", "hash", 10, "player2", [new("player1", mark.Id, null), new("player2", null, "greedy")]),
            Cancel);
        var accounts = await AccountsAsync();
        var stats = new StatsApi(_results, accounts, Resources);

        var read = Read(await stats.HandleAsync("GET", new AccountRequest(await SessionOfAsync(accounts, "MarkF"), Secure: false)));

        read.GetProperty("ranked").GetProperty("games").GetInt32().ShouldBe(1, "a game against a bot is not ranked");
        read.GetProperty("ranked").GetProperty("won").GetInt32().ShouldBe(1);
        read.GetProperty("streak").GetProperty("outcome").GetString().ShouldBe("won");
        read.TryGetProperty("creatures", out _).ShouldBeFalse();
        read.GetProperty("mutations").EnumerateArray().ShouldHaveSingleItem().GetProperty("name").GetString().ShouldBe("tier:gone:v1", "a mutation the catalogue no longer holds is shown by its id");
        var cast = read.GetProperty("spells").EnumerateArray().ShouldHaveSingleItem();
        cast.GetProperty("name").GetString().ShouldBe(spell.Name);
        cast.GetProperty("casts").GetInt32().ShouldBe(4);
    }

    [Fact]
    public async Task Nobody_signed_in_is_asked_to_sign_in()
    {
        var stats = new StatsApi(_results, await AccountsAsync(), Resources);

        var refused = await stats.HandleAsync("GET", new AccountRequest(null, Secure: false));

        refused.Status.ShouldBe(401);
        Read(refused).GetProperty("error").GetString().ShouldBe("Account.SignInRequired");
    }

    [Fact]
    public async Task A_player_who_played_nothing_reads_an_empty_record()
    {
        await PlayerAsync("MarkF");
        var accounts = await AccountsAsync();
        var stats = new StatsApi(_results, accounts, Resources);

        var read = Read(await stats.HandleAsync("GET", new AccountRequest(await SessionOfAsync(accounts, "MarkF"), Secure: false)));

        read.GetProperty("ranked").GetProperty("games").GetInt32().ShouldBe(0);
        read.GetProperty("streak").ValueKind.ShouldBe(JsonValueKind.Null);
        read.GetProperty("mutations").GetArrayLength().ShouldBe(0);
    }
}
