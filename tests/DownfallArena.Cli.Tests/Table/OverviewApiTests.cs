using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.Hosting;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// Headquarters' Overview (admin plan, A5): the numbers at a glance, from the sources the other tabs read, none for a
/// source the host does not have, and an empty card for one that does not answer.
/// </summary>
public sealed class OverviewApiTests : IDisposable
{
    private const string Token = "token-of-the-operator";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"overview-{Guid.CreateVersion7():N}");
    private readonly HostedTables _hosted = new();
    private readonly TableRegistry _registry;

    public OverviewApiTests()
    {
        _registry = _hosted.Registry();
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

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private AdminApi Admin(OverviewApi overview) =>
        new(_registry, _hosted.Composer, OperatorGate.WithToken(Token), TimeProvider.System, "Rules") { Overview = overview };

    private static async Task<JsonElement> OverviewAsync(AdminApi admin)
    {
        var answer = await admin.HandleAsync("GET", OverviewApi.Route, "", Token, principal: null);
        answer.Status.ShouldBe(200);
        return Json(answer);
    }

    [Fact]
    public async Task A_host_with_no_account_and_no_job_says_none_for_them_and_counts_its_tables()
    {
        var admin = Admin(new OverviewApi(_registry, _hosted.Composer, TimeProvider.System));

        var overview = await OverviewAsync(admin);

        overview.GetProperty("players").ValueKind.ShouldBe(JsonValueKind.Null);
        overview.GetProperty("registration").ValueKind.ShouldBe(JsonValueKind.Null);
        overview.GetProperty("games").GetProperty("finished").ValueKind.ShouldBe(JsonValueKind.Null);
        overview.GetProperty("jobs").GetProperty("lastRun").ValueKind.ShouldBe(JsonValueKind.Null);
        overview.GetProperty("games").GetProperty("inProgress").GetProperty("againstBots").GetInt32().ShouldBe(0);
        overview.GetProperty("waiting").GetProperty("openSeats").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task A_game_between_bots_under_way_is_counted_in_progress_against_bots()
    {
        var admin = Admin(new OverviewApi(_registry, _hosted.Composer, TimeProvider.System));
        (await admin.HandleAsync("POST", "/api/tables", """{"player1":"greedy","player2":"random"}""", Token, principal: null)).Status.ShouldBe(201);

        var games = (await OverviewAsync(admin)).GetProperty("games").GetProperty("inProgress");

        games.GetProperty("againstBots").GetInt32().ShouldBe(1);
        games.GetProperty("againstPlayers").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Registration_closed_is_said_and_a_results_store_that_does_not_answer_leaves_only_its_card_empty()
    {
        var settings = new HostSettingsCache(new FileHostSettingsStore(_root), TimeProvider.System);
        await settings.SaveAsync(new HostSettings(RegistrationOpen: false, Notice: null, "op", DateTimeOffset.UtcNow));
        var admin = Admin(new OverviewApi(_registry, _hosted.Composer, TimeProvider.System) { Settings = settings, Results = new Out() });

        var overview = await OverviewAsync(admin);

        overview.GetProperty("registration").GetProperty("open").GetBoolean().ShouldBeFalse();
        overview.GetProperty("games").GetProperty("finished").GetProperty("unavailable").GetBoolean().ShouldBeTrue();
        overview.GetProperty("games").GetProperty("inProgress").TryGetProperty("unavailable", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task The_overview_is_the_operator_s_alone()
    {
        var admin = Admin(new OverviewApi(_registry, _hosted.Composer, TimeProvider.System));

        (await admin.HandleAsync("GET", OverviewApi.Route, "", "a-seat-s-token", principal: null)).Status.ShouldBe(403);
    }

    /// <summary>A results store that is out, as one whose storage account does not answer.</summary>
    private sealed class Out : IMatchResults
    {
        public Task RecordAsync(MatchResult result, CancellationToken cancellationToken = default) => throw new IOException("The results store is out.");

        public Task<IReadOnlyList<MatchResult>> ListAsync(CancellationToken cancellationToken = default) => throw new IOException("The results store is out.");
    }
}
