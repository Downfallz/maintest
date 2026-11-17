using System.IO.Compression;
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
/// A bug report on its way to a public issue (docs/tabletop/feedback-plan.md, R5): the scenario its Reproduce cut,
/// downloaded redacted as a zip to attach to the issue, the issue's number kept as <i>filed #N</i>, and <i>fixed</i> once
/// the issue is closed. The matches are greedy against greedy, three creatures a side.
/// </summary>
public sealed class FeedbackIssueTests : IDisposable
{
    private const string Account = "acct_0123456789abcdef";

    private static readonly RuleSet ThreeASide = RuleSet.Create(3, 2, 1, 6, 2.0);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"feedback-issue-{Guid.CreateVersion7():N}");
    private readonly HostedTables _hosted = new(recording: true, rules: ThreeASide);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly FileFeedbackStore _feedback;
    private readonly FeedbackWrites _writes = new();
    private readonly OperatorActions _actions;
    private readonly ScenarioStore _scenarios;

    public FeedbackIssueTests()
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

    private FeedbackAdminApi Tab() =>
        new(new OperatorFeedback(_feedback, new FeedbackMutes(null), _writes, _clock), _hosted.Stored, _clock)
        {
            Notes = new OperatorNotes(_actions),
            Scenarios = new ScenarioAdminApi(_scenarios, _hosted.Stored!, _hosted.Composer),
        };

    /// <summary>Asks the tab as the operator, then moves the clock a second, so the Activity lines sort as they were noted.</summary>
    private async Task<StudioResponse> AskAsync(string method, string id, string suffix = "", object? body = null)
    {
        var path = suffix.Length == 0 ? $"{FeedbackAdminApi.Route}/{id}" : $"{FeedbackAdminApi.Route}/{id}/{suffix}";
        var answer = await Tab().HandleAsync(method, path, body is null ? string.Empty : JsonSerializer.Serialize(body), "mark");
        _clock.Advance(TimeSpan.FromSeconds(1));
        return answer;
    }

    private async Task<string[]> ActivityAsync() =>
        [.. (await _actions.PageAsync(1, Cancel)).Actions.Select(action => $"{action.Action} {action.Target} {action.Detail}".TrimEnd())];

    private static string[] Actions(JsonElement sheet) => [.. sheet.GetProperty("actions").EnumerateArray().Select(one => one.GetString()!)];

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

    /// <summary>A bug report kept as the host keeps one a signed-in player sent from a seat.</summary>
    private async Task<Feedback> ReportedAsync(FeedbackContext? context, FeedbackKind kind = FeedbackKind.Bug)
    {
        var report = new Feedback
        {
            Id = Feedback.NewId(_clock.GetUtcNow()),
            Kind = kind,
            At = _clock.GetUtcNow(),
            Sender = Account,
            Account = Account,
            Category = kind == FeedbackKind.Bug ? "rules" : "battle",
            Text = "The bleed did not tick after the stun.",
            Context = context,
        };
        await _feedback.SaveAsync(report, Cancel);
        _clock.Advance(TimeSpan.FromSeconds(1));
        return report;
    }

    /// <summary>A report on a finished table's round 2, reproduced, written when two of the round's entries were journaled.</summary>
    private async Task<(Feedback Report, string Table, ScenarioDocument Cut, string Scenario)> ReproducedAsync()
    {
        var table = await PlayedAsync();
        var report = await ReportedAsync(new FeedbackContext { Table = table, Seat = "player2", Round = 2, SubPhase = "Command" });
        var sheet = Read(await AskAsync("POST", report.Id, "reproduce"));
        var scenario = sheet.GetProperty("reproduction").GetProperty("id").GetString()!;
        var cut = (await _scenarios.GetAsync(scenario, Cancel)).ShouldNotBeNull();
        var written = (await _feedback.FindAsync(report.Id, Cancel))!;
        written = written with { Context = written.Context! with { JournalLength = cut.Scenario.Cut.CutAt + 2 } };
        await _feedback.SaveAsync(written, Cancel);
        return (written, table, cut, scenario);
    }

    private static string Unzipped(StudioResponse answer)
    {
        using var zip = new ZipArchive(new MemoryStream(answer.Body), ZipArchiveMode.Read);
        var entry = zip.Entries.ShouldHaveSingleItem();
        entry.FullName.ShouldBe("scenario.json");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task A_reproduced_report_downloads_as_a_zip_holding_one_redacted_scenario()
    {
        var (report, table, cut, scenario) = await ReproducedAsync();

        var answer = await AskAsync("GET", report.Id, "scenario.zip");

        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        answer.ContentType.ShouldBe("application/zip");
        answer.Headers.ShouldNotBeNull().ShouldContain(new KeyValuePair<string, string>("Content-Disposition", $"attachment; filename=\"report-{report.Id}.zip\""));
        var text = Unzipped(answer);
        text.ShouldNotContain(table);
        text.ShouldNotContain(cut.Scenario.Source.MatchId.ToString());
        var file = ScenarioFile.Read(text);
        (file.Session, file.Scenario.Source.Run, file.Scenario.Source.MatchId).ShouldBe((report.Id, report.Id, RedactedScenario.MatchOf(report.Id)));
        (file.Scenario.Cut, file.Seat, file.Title, file.Goal).ShouldBe((cut.Scenario.Cut, PlayerSlot.Player2, $"Report {report.Id}, round 2", null));
        file.Scenario.After.Count.ShouldBe(2);
        file.Schema.ShouldNotBeNull();
        RedactedScenario.Check(text, report.Id, [("the table", table)]).IsSuccess.ShouldBeTrue();
        (await ActivityAsync()).ShouldContain($"feedback.download {report.Id} {scenario}");
    }

    [Fact]
    public async Task The_downloaded_scenario_holds_on_this_engine_with_the_content_it_carries()
    {
        var (report, _, _, _) = await ReproducedAsync();
        var path = Path.Combine(_root, "scenario.json");
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(path, Unzipped(await AskAsync("GET", report.Id, "scenario.zip")), Cancel);

        var held = await ScenarioCommands.CheckAsync(new CliOptions { Command = CliOptions.ScenarioCheckCommand, Output = "unused", Scenario = path, SchemaPath = Path.Combine(_root, "none.json") });

        held.ShouldBe(0);
    }

    [Fact]
    public async Task A_scenario_that_still_holds_the_reporter_s_account_is_not_offered()
    {
        var (report, _, cut, _) = await ReproducedAsync();
        var named = await _scenarios.PutAsync(cut with { Title = $"Sent by {Account}" }, Cancel);
        await _feedback.SaveAsync(report with { Reproduction = named }, Cancel);

        var refused = await AskAsync("GET", report.Id, "scenario.zip");

        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((409, "Feedback.NotRedacted"));
        Read(refused).GetProperty("message").GetString().ShouldBe("The scenario still holds an account: it is not offered.");
        (await ActivityAsync()).ShouldNotContain(line => line.StartsWith("feedback.download", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_report_not_reproduced_and_an_idea_offer_no_scenario()
    {
        var report = await ReportedAsync(new FeedbackContext { Table = await PlayedAsync(), Seat = "player1", Round = 2 });
        var idea = await ReportedAsync(new FeedbackContext { Table = "20261116-120000-deadbeef", Seat = "player1", Round = 2 }, FeedbackKind.Idea);

        var (notReproduced, notABug) = (await AskAsync("GET", report.Id, "scenario.zip"), await AskAsync("GET", idea.Id, "scenario.zip"));

        (notReproduced.Status, Read(notReproduced).GetProperty("error").GetString()).ShouldBe((409, "Feedback.NotReproduced"));
        (notABug.Status, Read(notABug).GetProperty("error").GetString()).ShouldBe((409, "Feedback.NotABug"));
        Actions(Read(await AskAsync("GET", report.Id))).ShouldNotContain("download");
    }

    [Fact]
    public async Task A_report_from_a_table_still_under_way_offers_no_scenario_until_it_is_over()
    {
        using var registry = _hosted.Registry();
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        var seat = table.Seats[0];
        (await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token)).Status.ShouldBe(200);
        var report = await ReportedAsync(new FeedbackContext { Table = table.Id, Seat = seat.Name, Round = 1 });
        await _feedback.SaveAsync(report with { Reproduction = "sc-20261116-120000-0a1b2c3d" }, Cancel);

        var refused = await AskAsync("GET", report.Id, "scenario.zip");

        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((409, "Feedback.UnderWay"));
        Actions(Read(await AskAsync("GET", report.Id))).ShouldNotContain("download");
    }

    [Fact]
    public async Task The_sheet_offers_the_download_once_reproduced_and_filing_while_the_report_is_open()
    {
        var (report, _, _, _) = await ReproducedAsync();

        var sheet = Read(await AskAsync("GET", report.Id));

        Actions(sheet).ShouldContain("download");
        Actions(sheet).ShouldContain("file");
        sheet.GetProperty("repository").GetString().ShouldBe(FeedbackAdminApi.Repository);
        sheet.GetProperty("issue").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_report_is_filed_with_its_issue_s_number_once_and_noted_once()
    {
        var report = await ReportedAsync(new FeedbackContext { Table = "20261116-120000-deadbeef", Seat = "player1", Round = 2 });

        var filed = Read(await AskAsync("PUT", report.Id, "status", new { status = "filed", issue = 731 }));
        await AskAsync("PUT", report.Id, "status", new { status = "filed", issue = 731 });

        (filed.GetProperty("status").GetString(), filed.GetProperty("issue").GetInt32()).ShouldBe(("filed", 731));
        var list = Read(await Tab().HandleAsync("GET", FeedbackAdminApi.Route, string.Empty, "mark", "status=filed"));
        list.GetProperty("repository").GetString().ShouldBe(FeedbackAdminApi.Repository);
        var row = list.GetProperty("feedback").EnumerateArray().ShouldHaveSingleItem();
        (row.GetProperty("id").GetString(), row.GetProperty("issue").GetInt32()).ShouldBe((report.Id, 731));
        (await ActivityAsync()).ShouldBe([$"feedback.file {report.Id} #731"]);
    }

    [Fact]
    public async Task Filing_names_a_number()
    {
        var report = await ReportedAsync(new FeedbackContext { Table = "20261116-120000-deadbeef", Seat = "player1", Round = 2 });

        var refused = await AskAsync("PUT", report.Id, "status", new { status = "filed" });

        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((400, "Feedback.Invalid"));
    }

    [Fact]
    public async Task A_filed_report_is_fixed_by_its_issue_closing_and_one_never_filed_is_not()
    {
        var filed = await ReportedAsync(new FeedbackContext { Table = "20261116-120000-deadbeef", Seat = "player1", Round = 2 });
        var unfiled = await ReportedAsync(new FeedbackContext { Table = "20261116-120000-deadbeef", Seat = "player1", Round = 3 });
        await AskAsync("PUT", filed.Id, "status", new { status = "filed", issue = 731 });

        var fixedReport = Read(await AskAsync("PUT", filed.Id, "status", new { status = "fixed" }));
        var refused = await AskAsync("PUT", unfiled.Id, "status", new { status = "fixed" });

        (fixedReport.GetProperty("status").GetString(), fixedReport.GetProperty("issue").GetInt32()).ShouldBe(("fixed", 731));
        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((409, "Feedback.NotFiled"));
        (await ActivityAsync()).ShouldBe([$"feedback.status {filed.Id} fixed: #731", $"feedback.file {filed.Id} #731"]);
    }
}
