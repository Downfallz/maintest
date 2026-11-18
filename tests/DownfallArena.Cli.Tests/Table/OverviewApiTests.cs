using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.Application.Hosting;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.FeedbackBox;

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

    private async Task<PlayedTable> OpenAsync(AdminApi admin, string request)
    {
        var opened = await admin.HandleAsync("POST", "/api/tables", request, Token, principal: null);
        opened.Status.ShouldBe(201);
        return (await _registry.ByIdAsync(Json(opened).GetProperty("id").GetString()!, TestContext.Current.CancellationToken)).ShouldNotBeNull();
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

    /// <summary>
    /// A person who has reached their seat against a bot holds the game in progress: the bot waits for their answer.
    /// Two bots would not do: they play without waiting, and their game can end before the overview is read (#598).
    /// </summary>
    [Fact]
    public async Task A_game_against_a_bot_under_way_is_counted_in_progress_against_bots()
    {
        var admin = Admin(new OverviewApi(_registry, _hosted.Composer, TimeProvider.System));
        var table = await OpenAsync(admin, """{"player1":"person","player2":"greedy"}""");
        table.Seats[0].Person!.Arrive();
        table.Session.Begin();

        var games = (await OverviewAsync(admin)).GetProperty("games").GetProperty("inProgress");

        table.IsOver.ShouldBeFalse("the bot waits for the person, so the game is still under way");
        games.GetProperty("againstBots").GetInt32().ShouldBe(1);
        games.GetProperty("againstPlayers").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task A_game_between_bots_that_has_ended_is_no_longer_counted_in_progress()
    {
        var admin = Admin(new OverviewApi(_registry, _hosted.Composer, TimeProvider.System));
        var table = await OpenAsync(admin, """{"player1":"greedy","player2":"random"}""");
        (await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue("the bots played their game to its end");

        var games = (await OverviewAsync(admin)).GetProperty("games").GetProperty("inProgress");

        games.GetProperty("againstBots").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Feedback_nobody_has_looked_at_is_counted_with_the_bug_reports_among_it()
    {
        var store = new FileFeedbackStore(Path.Combine(_root, "feedback"));
        var now = DateTimeOffset.UtcNow;
        await store.SaveAsync(new Feedback { Id = Feedback.NewId(now), Kind = FeedbackKind.Bug, At = now, Sender = "seat-a", Category = "rules", Text = "The bleed did not tick." }, TestContext.Current.CancellationToken);
        await store.SaveAsync(new Feedback { Id = Feedback.NewId(now.AddSeconds(-1)), Kind = FeedbackKind.Idea, At = now.AddSeconds(-1), Sender = "seat-a", Category = "battle", Text = "Show the last round again." }, TestContext.Current.CancellationToken);
        var admin = Admin(new OverviewApi(_registry, _hosted.Composer, TimeProvider.System) { Feedback = new CountWaitingFeedback(store, TimeProvider.System) });

        var feedback = (await OverviewAsync(admin)).GetProperty("feedback");

        (feedback.GetProperty("new").GetInt32(), feedback.GetProperty("newBugs").GetInt32(), feedback.GetProperty("lastDay").GetInt32(), feedback.GetProperty("full").GetBoolean()).ShouldBe((2, 1, 2, false));
    }

    [Fact]
    public async Task A_feedback_store_that_does_not_answer_leaves_only_its_card_empty()
    {
        var admin = Admin(new OverviewApi(_registry, _hosted.Composer, TimeProvider.System) { Feedback = new CountWaitingFeedback(new Unreadable(), TimeProvider.System) });

        var overview = await OverviewAsync(admin);

        overview.GetProperty("feedback").GetProperty("unavailable").GetBoolean().ShouldBeTrue();
        overview.GetProperty("waiting").TryGetProperty("unavailable", out _).ShouldBeFalse();
    }

    /// <summary>A feedback store whose storage does not answer.</summary>
    private sealed class Unreadable : IFeedbackStore
    {
        public Task SaveAsync(Feedback feedback, CancellationToken cancellationToken = default) => throw new IOException("The disk is gone.");

        public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default) => throw new IOException("The disk is gone.");

        public Task<IReadOnlyList<string>> IdsSentAsync(DateTimeOffset after, DateTimeOffset until, CancellationToken cancellationToken = default) => throw new IOException("The disk is gone.");

        public Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default) => throw new IOException("The disk is gone.");

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => throw new IOException("The disk is gone.");
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
