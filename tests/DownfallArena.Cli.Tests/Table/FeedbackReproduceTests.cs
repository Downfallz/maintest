using System.Text;
using System.Text.Json;
using DownfallArena.Application.Admin;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Admin;
using DownfallArena.Infrastructure.FeedbackBox;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches.Scenarios;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// Reproduce (docs/tabletop/feedback-plan.md, R4): a bug report's round cut as a match scenario in its sender's seat,
/// through the Scenarios tab's own cutting, from the table's recorded session or from a practice's copied entries, and
/// played from the sheet's link. The matches are greedy against greedy, three creatures a side.
/// </summary>
public sealed class FeedbackReproduceTests : IDisposable
{
    private static readonly RuleSet ThreeASide = RuleSet.Create(3, 2, 1, 6, 2.0);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"feedback-reproduce-{Guid.CreateVersion7():N}");
    private readonly HostedTables _hosted = new(recording: true, rules: ThreeASide);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly FileFeedbackStore _feedback;
    private readonly FeedbackWrites _writes = new();
    private readonly OperatorActions _actions;
    private readonly ScenarioStore _scenarios;

    public FeedbackReproduceTests()
    {
        _feedback = new FileFeedbackStore(Path.Combine(_root, "feedback"));
        _actions = new OperatorActions(new FileOperatorLog(Path.Combine(_root, "log")), _clock);
        _scenarios = new ScenarioStore(new FileArtifactStore(Path.Combine(_root, "scenarios")), _clock);
    }

    public void Dispose()
    {
        _writes.Dispose();
        _hosted.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private ScenarioAdminApi Cutting() => new(_scenarios, _hosted.Stored!, _hosted.Composer);

    private FeedbackAdminApi Tab() =>
        new(new OperatorFeedback(_feedback, new FeedbackMutes(null), _writes, _clock), _hosted.Stored, _clock) { Notes = new OperatorNotes(_actions), Scenarios = Cutting() };

    private async Task<StudioResponse> ReproduceAsync(string id)
    {
        var answer = await Tab().HandleAsync("POST", $"{FeedbackAdminApi.Route}/{id}/reproduce", string.Empty, "mark");
        _clock.Advance(TimeSpan.FromSeconds(1));
        return answer;
    }

    private async Task<JsonElement> SheetAsync(string id) => Read(await Tab().HandleAsync("GET", $"{FeedbackAdminApi.Route}/{id}", string.Empty, "mark"));

    private async Task<string[]> ActivityAsync() =>
        [.. (await _actions.PageAsync(1, Cancel)).Actions.Select(action => $"{action.Action} {action.Target} {action.Detail}".TrimEnd())];

    /// <summary>A table of two greedy bots, three creatures a side, played to its end and recorded.</summary>
    private async Task<string> PlayedAsync()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.Bots, Cancel);
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), Cancel);
        for (var waited = 0; !table.IsFinished && waited < 300; waited++)
        {
            await Task.Delay(100, Cancel);
        }

        await table.RecordAsync(Cancel);
        return table.Id;
    }

    /// <summary>A bug report kept as the host keeps one sent from a seat: the words, and where it was sent from.</summary>
    private async Task<string> ReportedAsync(FeedbackContext? context, FeedbackKind kind = FeedbackKind.Bug)
    {
        var report = new Feedback
        {
            Id = Feedback.NewId(_clock.GetUtcNow()),
            Kind = kind,
            At = _clock.GetUtcNow(),
            Sender = "seat-0123456789abcdef",
            Category = kind == FeedbackKind.Bug ? "rules" : "battle",
            Text = "The bleed did not tick after the stun.",
            Context = context,
        };
        await _feedback.SaveAsync(report, Cancel);
        _clock.Advance(TimeSpan.FromSeconds(1));
        return report.Id;
    }

    private async Task<ScenarioDocument> ScenarioAsync(JsonElement sheet) =>
        (await _scenarios.GetAsync(sheet.GetProperty("reproduction").GetProperty("id").GetString()!, Cancel)).ShouldNotBeNull();

    [Fact]
    public async Task A_report_from_a_recorded_match_is_cut_at_its_round_in_its_seat_and_opens_as_a_practice_on_its_board()
    {
        var session = await PlayedAsync();
        var id = await ReportedAsync(new FeedbackContext { Table = session, Seat = "player2", Round = 2, SubPhase = "Command" });

        var answer = await ReproduceAsync(id);

        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        var sheet = Read(answer);
        var scenario = sheet.GetProperty("reproduction").GetProperty("id").GetString()!;
        sheet.GetProperty("reproduction").GetProperty("link").GetString().ShouldBe($"/battle?scenario={scenario}");
        sheet.GetProperty("status").GetString().ShouldBe("new");
        var document = await ScenarioAsync(sheet);
        (document.Scenario.Cut.Round, document.Seat, document.Session, document.Title, document.Goal).ShouldBe((2, PlayerSlot.Player2, session, $"Report {id}, round 2", null));
        await using var practices = new ScenarioPractices(_hosted.Composer, _scenarios, _clock);
        var practice = (await practices.OpenAsync(scenario, "operator", Cancel)).Opened.ShouldNotBeNull();
        var board = Read(await practice.Play.Api.HandleAsync("GET", "/api/seat/player2", "", practice.Play.Token)).GetProperty("board");
        (board.GetProperty("roundNumber").GetInt32(), board.GetProperty("allies").GetArrayLength()).ShouldBe((2, 3));
        (await ActivityAsync()).ShouldBe([$"feedback.reproduce {id} {scenario}, round 2, player2"]);
    }

    /// <summary>The tab as the table host composes it (#737): its Reproduce reaches the Scenarios tab's cutting.</summary>
    [Fact]
    public async Task The_feedback_tab_the_host_composes_offers_and_cuts_a_report_s_round()
    {
        var id = await ReportedAsync(new FeedbackContext { Table = await PlayedAsync(), Seat = "player2", Round = 2 });
        var (_, composed) = TableHost.CuttingTabs(_scenarios, _hosted.Stored, _hosted.Composer, new OperatorFeedback(_feedback, new FeedbackMutes(null), _writes, _clock), new OperatorNotes(_actions), _clock);
        var tab = composed.ShouldNotBeNull();

        var sheet = Read(await tab.HandleAsync("GET", $"{FeedbackAdminApi.Route}/{id}", string.Empty, "mark"));
        var cut = await tab.HandleAsync("POST", $"{FeedbackAdminApi.Route}/{id}/reproduce", string.Empty, "mark");

        sheet.GetProperty("actions").EnumerateArray().Select(one => one.GetString()).ShouldContain("reproduce");
        cut.Status.ShouldBe(200, Encoding.UTF8.GetString(cut.Body));
        Read(cut).GetProperty("reproduction").GetProperty("link").GetString().ShouldStartWith("/battle?scenario=");
    }

    [Fact]
    public async Task Reproduce_asked_again_answers_the_scenario_it_cut_and_notes_nothing_more()
    {
        var id = await ReportedAsync(new FeedbackContext { Table = await PlayedAsync(), Seat = "player1", Round = 2 });
        var first = Read(await ReproduceAsync(id)).GetProperty("reproduction").GetProperty("id").GetString();

        var again = Read(await ReproduceAsync(id)).GetProperty("reproduction").GetProperty("id").GetString();

        again.ShouldBe(first);
        (await _scenarios.ListAsync(Cancel)).Count.ShouldBe(1);
        (await ActivityAsync()).Length.ShouldBe(1);
    }

    [Fact]
    public async Task Two_presses_at_once_leave_one_scenario_and_one_line()
    {
        var id = await ReportedAsync(new FeedbackContext { Table = await PlayedAsync(), Seat = "player1", Round = 2 });
        var tab = Tab();

        var answers = await Task.WhenAll(
            tab.HandleAsync("POST", $"{FeedbackAdminApi.Route}/{id}/reproduce", string.Empty, "mark"),
            tab.HandleAsync("POST", $"{FeedbackAdminApi.Route}/{id}/reproduce", string.Empty, "ops"));

        var kept = (await _scenarios.ListAsync(Cancel)).ShouldHaveSingleItem().Id;
        answers.Select(answer => Read(answer).GetProperty("reproduction").GetProperty("id").GetString()).ShouldBe([kept, kept]);
        (await ActivityAsync()).Length.ShouldBe(1);
    }

    [Fact]
    public async Task Reproduce_is_the_operator_s_alone()
    {
        var id = await ReportedAsync(new FeedbackContext { Table = await PlayedAsync(), Seat = "player1", Round = 2 });
        using var registry = _hosted.Registry();
        var admin = new AdminApi(registry, _hosted.Composer, OperatorGate.WithToken("operator-token"), _clock, "Rules", _hosted.Stored) { Feedback = Tab() };

        var refused = await admin.HandleAsync("POST", $"{FeedbackAdminApi.Route}/{id}/reproduce", string.Empty, "someone-else", principal: null);

        refused.Status.ShouldBe(403);
        (await _scenarios.ListAsync(Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_report_whose_scenario_was_removed_since_is_cut_again()
    {
        var id = await ReportedAsync(new FeedbackContext { Table = await PlayedAsync(), Seat = "player1", Round = 2 });
        var first = Read(await ReproduceAsync(id)).GetProperty("reproduction").GetProperty("id").GetString()!;
        (await Cutting().HandleAsync("DELETE", $"{ScenarioAdminApi.Route}/{first}", "", "mark")).Status.ShouldBe(204);

        var again = Read(await ReproduceAsync(id)).GetProperty("reproduction").GetProperty("id").GetString()!;

        again.ShouldNotBe(first);
        (await _scenarios.GetAsync(again, Cancel)).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_round_the_journal_does_not_offer_is_answered_with_the_rounds_it_does()
    {
        var id = await ReportedAsync(new FeedbackContext { Table = await PlayedAsync(), Seat = "player1", Round = 99 });

        var refused = await ReproduceAsync(id);

        refused.Status.ShouldBe(409);
        var body = Read(refused);
        body.GetProperty("error").GetString().ShouldBe("Scenario.RoundUnavailable");
        body.GetProperty("rounds").EnumerateArray().Select(one => one.GetInt32()).ShouldContain(2);
        (await SheetAsync(id)).GetProperty("reproduction").ValueKind.ShouldBe(JsonValueKind.Null);
        (await ActivityAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_report_from_a_practice_is_cut_from_its_scenario_and_the_entries_it_copied()
    {
        var session = await PlayedAsync();
        var practised = Read(await Cutting().HandleAsync("POST", ScenarioAdminApi.Route, JsonSerializer.Serialize(new { session, round = 2, seat = "player2" }), "mark")).GetProperty("id").GetString()!;
        var opened = (await _scenarios.GetAsync(practised, Cancel)).ShouldNotBeNull();
        var straight = Read(await Cutting().HandleAsync("POST", ScenarioAdminApi.Route, JsonSerializer.Serialize(new { session, round = 3, seat = "player2" }), "mark")).GetProperty("id").GetString()!;

        // A practice played as the recorded match went on: what its report copies is the source's entries after the cut.
        var id = await ReportedAsync(new FeedbackContext { Scenario = practised, Seat = "player2", Round = 3, PracticeEntries = ScenarioFile.JournalLinesOf(opened.Scenario.After) });
        var answer = await ReproduceAsync(id);

        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        var document = await ScenarioAsync(Read(answer));
        (document.Scenario.Cut.Round, document.Seat, document.Session).ShouldBe((3, PlayerSlot.Player2, null));
        document.Scenario.Checkpoint.ShouldBe((await _scenarios.GetAsync(straight, Cancel)).ShouldNotBeNull().Scenario.Checkpoint);
    }

    [Fact]
    public async Task A_report_from_a_practice_played_here_reads_back_what_it_copied()
    {
        var session = await PlayedAsync();
        var practised = Read(await Cutting().HandleAsync("POST", ScenarioAdminApi.Route, JsonSerializer.Serialize(new { session, round = 2, seat = "player2" }), "mark")).GetProperty("id").GetString()!;
        await using var practices = new ScenarioPractices(_hosted.Composer, _scenarios, _clock);
        var play = (await practices.OpenAsync(practised, "learner", Cancel)).Opened.ShouldNotBeNull().Play;
        var view = Read(await play.Api.HandleAsync("GET", "/api/seat/player2", "", play.Token));
        var creature = view.GetProperty("options").GetProperty("speed").GetProperty("missing")[0].GetInt32();
        var sent = await play.Api.HandleAsync("POST", "/api/seat/player2/decision", JsonSerializer.Serialize(new { asked = view.GetProperty("waitingAsked").GetInt64(), kind = "Speed", creature, speed = "Quick" }), play.Token);
        sent.Status.ShouldBe(204, Encoding.UTF8.GetString(sent.Body));
        var copied = ScenarioFile.JournalLinesOf(await play.SinceTheCutAsync(Cancel));
        var id = await ReportedAsync(new FeedbackContext { Scenario = practised, Seat = "player2", Round = 2, PracticeEntries = copied });

        var answer = await ReproduceAsync(id);

        copied.ShouldNotBeEmpty();
        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        var document = await ScenarioAsync(Read(answer));
        document.Scenario.After.Count.ShouldBe(copied.Count);
        document.Scenario.Checkpoint.ShouldBe((await _scenarios.GetAsync(practised, Cancel)).ShouldNotBeNull().Scenario.Checkpoint);
    }

    [Fact]
    public async Task A_report_from_a_practice_that_copied_nothing_offers_only_the_practice_s_own_round()
    {
        var session = await PlayedAsync();
        var practised = Read(await Cutting().HandleAsync("POST", ScenarioAdminApi.Route, JsonSerializer.Serialize(new { session, round = 2, seat = "player2" }), "mark")).GetProperty("id").GetString()!;
        var id = await ReportedAsync(new FeedbackContext { Scenario = practised, Seat = "player2", Round = 3 });

        var refused = await ReproduceAsync(id);

        refused.Status.ShouldBe(409);
        Read(refused).GetProperty("rounds").EnumerateArray().Select(one => one.GetInt32()).ShouldContain(2);
        Read(refused).GetProperty("rounds").EnumerateArray().Select(one => one.GetInt32()).ShouldNotContain(3);
    }

    [Fact]
    public async Task An_idea_and_a_report_sent_outside_a_game_are_not_reproduced()
    {
        var idea = await ReportedAsync(new FeedbackContext { Table = "20261116-120000-deadbeef", Seat = "player1", Round = 2 }, FeedbackKind.Idea);
        var outside = await ReportedAsync(context: null);

        var (notABug, nowhere) = (await ReproduceAsync(idea), await ReproduceAsync(outside));

        (notABug.Status, Read(notABug).GetProperty("error").GetString()).ShouldBe((409, "Feedback.NotABug"));
        (nowhere.Status, Read(nowhere).GetProperty("error").GetString()).ShouldBe((409, "Feedback.NoPosition"));
        (await SheetAsync(idea)).GetProperty("actions").EnumerateArray().Select(one => one.GetString()).ShouldNotContain("reproduce");
        (await SheetAsync(outside)).GetProperty("actions").EnumerateArray().Select(one => one.GetString()).ShouldNotContain("reproduce");
    }

    [Fact]
    public async Task A_report_that_does_not_name_its_seat_is_not_cut_in_another()
    {
        var id = await ReportedAsync(new FeedbackContext { Table = await PlayedAsync(), Round = 2 });

        var refused = await ReproduceAsync(id);

        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((409, "Feedback.NoPosition"));
        (await SheetAsync(id)).GetProperty("actions").EnumerateArray().Select(one => one.GetString()).ShouldNotContain("reproduce");
        (await _scenarios.ListAsync(Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_scenario_removed_since_is_linked_no_more()
    {
        var id = await ReportedAsync(new FeedbackContext { Table = await PlayedAsync(), Seat = "player1", Round = 2 });
        var cut = Read(await ReproduceAsync(id)).GetProperty("reproduction").GetProperty("id").GetString()!;

        (await Cutting().HandleAsync("DELETE", $"{ScenarioAdminApi.Route}/{cut}", "", "mark")).Status.ShouldBe(204);

        (await SheetAsync(id)).GetProperty("reproduction").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>
    /// A report from a table still played is not cut (#736), as its scenario is not downloaded (#744): the table's journal
    /// holds the other seat's choices of the round, face down. Its sheet offers Reproduce once the game is over.
    /// </summary>
    [Fact]
    public async Task A_report_from_a_table_still_under_way_is_not_cut_until_it_is_over()
    {
        using var registry = _hosted.Registry();
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        var seat = table.Seats[0];
        (await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token)).Status.ShouldBe(200);
        var id = await ReportedAsync(new FeedbackContext { Table = table.Id, Seat = seat.Name, Round = 1 });

        var refused = await ReproduceAsync(id);

        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((409, "Feedback.UnderWay"));
        (await _scenarios.ListAsync(Cancel)).ShouldBeEmpty();
        (await ActivityAsync()).ShouldBeEmpty();
        (await SheetAsync(id)).GetProperty("actions").EnumerateArray().Select(one => one.GetString()).ShouldNotContain("reproduce");
    }

    [Fact]
    public async Task A_report_from_a_round_offers_reproduce_on_its_sheet()
    {
        var id = await ReportedAsync(new FeedbackContext { Table = "20261116-120000-deadbeef", Seat = "player1", Round = 2 });

        var actions = (await SheetAsync(id)).GetProperty("actions").EnumerateArray().Select(one => one.GetString());

        actions.ShouldContain("reproduce");
    }
}
