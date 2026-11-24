using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DownfallArena.Application.Admin;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Infrastructure.Admin;
using DownfallArena.Infrastructure.FeedbackBox;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches.Scenarios;
using DownfallArena.SharedKernel.Identifiers;
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
        new(FeedbackDesk.Open(new FeedbackIndex(_feedback, _feedback, _clock), new FeedbackMutes(null), _writes, _clock), _hosted.Stored, _clock)
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
        await Waits.UntilAsync(() => table.IsFinished, "the table finished", Cancel);

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

        var answer = await AskAsync("POST", report.Id, "scenario.zip");

        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        answer.ContentType.ShouldBe("application/zip");
        answer.Headers.ShouldNotBeNull().ShouldContain(new KeyValuePair<string, string>("Content-Disposition", $"attachment; filename=\"report-{report.Id}.zip\""));
        answer.Headers.ShouldContain(new KeyValuePair<string, string>("Cache-Control", "no-store, private"));
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
        await File.WriteAllTextAsync(path, Unzipped(await AskAsync("POST", report.Id, "scenario.zip")), Cancel);

        var held = await ScenarioCommands.CheckAsync(new CliOptions { Command = CliOptions.ScenarioCheckCommand, Output = "unused", Scenario = path, SchemaPath = Path.Combine(_root, "none.json") });

        held.ShouldBe(0);
    }

    [Fact]
    public async Task A_scenario_that_still_holds_the_reporter_s_account_is_not_offered()
    {
        var (report, _, cut, _) = await ReproducedAsync();
        var named = await _scenarios.PutAsync(cut with { Title = $"Sent by {Account}" }, Cancel);
        await _feedback.SaveAsync(report with { Reproduction = named }, Cancel);

        var refused = await AskAsync("POST", report.Id, "scenario.zip");

        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((409, "Feedback.NotRedacted"));
        Read(refused).GetProperty("message").GetString().ShouldBe("The scenario still holds an account, in its title: it is not offered.");
        (await ActivityAsync()).ShouldNotContain(line => line.StartsWith("feedback.download", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_report_not_reproduced_and_an_idea_offer_no_scenario()
    {
        var report = await ReportedAsync(new FeedbackContext { Table = await PlayedAsync(), Seat = "player1", Round = 2 });
        var idea = await ReportedAsync(new FeedbackContext { Table = "20261116-120000-deadbeef", Seat = "player1", Round = 2 }, FeedbackKind.Idea);

        var (notReproduced, notABug) = (await AskAsync("POST", report.Id, "scenario.zip"), await AskAsync("POST", idea.Id, "scenario.zip"));

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

        var refused = await AskAsync("POST", report.Id, "scenario.zip");

        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((409, "Feedback.UnderWay"));
        Actions(Read(await AskAsync("GET", report.Id))).ShouldNotContain("download");
    }

    [Fact]
    public async Task A_report_from_a_practice_cut_from_a_table_still_under_way_offers_no_scenario()
    {
        using var registry = _hosted.Registry();
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        var seat = table.Seats[0];
        (await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token)).Status.ShouldBe(200);
        var (_, _, cut, practised) = await ReproducedAsync();

        // A practice's cut keeps no session, as Reproduce writes it: only its source's run names the table.
        var fromLiveTable = await _scenarios.PutAsync(cut with { Session = null, Scenario = cut.Scenario with { Source = cut.Scenario.Source with { Run = table.Id } } }, Cancel);
        var report = await ReportedAsync(new FeedbackContext { Scenario = practised, Seat = "player2", Round = 2 });
        await _feedback.SaveAsync(report with { Reproduction = fromLiveTable }, Cancel);

        var refused = await AskAsync("POST", report.Id, "scenario.zip");

        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((409, "Feedback.UnderWay"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_report_whose_table_record_cannot_be_read_offers_no_scenario(bool unparseable)
    {
        var (report, table, _, _) = await ReproducedAsync();
        var record = Path.Combine(_hosted.RunsDirectory, table, TableRecord.File);
        if (unparseable)
        {
            await File.WriteAllTextAsync(record, "{ not a record", Cancel);
        }
        else
        {
            File.Delete(record);
        }

        var refused = await AskAsync("POST", report.Id, "scenario.zip");

        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((409, "Feedback.TableUnread"));
        var sheet = Read(await AskAsync("GET", report.Id));
        (sheet.GetProperty("underWay").GetBoolean(), sheet.GetProperty("unread").GetBoolean()).ShouldBe((false, true));
        Actions(sheet).ShouldNotContain("download");
        Actions(sheet).ShouldNotContain("diagnose");
    }

    [Theory]
    [InlineData(true, "Feedback.TableUnread")]
    [InlineData(false, null)]
    public async Task A_practice_report_whose_source_table_record_cannot_be_parsed_offers_no_scenario_and_a_missing_one_passes(bool unparseable, string? refusal)
    {
        var (_, table, cut, practised) = await ReproducedAsync();
        var record = Path.Combine(_hosted.RunsDirectory, table, TableRecord.File);
        var fromTable = await _scenarios.PutAsync(cut with { Session = null, Scenario = cut.Scenario with { Source = cut.Scenario.Source with { Run = table } } }, Cancel);
        var report = await ReportedAsync(new FeedbackContext { Scenario = practised, Seat = "player2", Round = 2 });
        await _feedback.SaveAsync(report with { Reproduction = fromTable }, Cancel);
        if (unparseable)
        {
            await File.WriteAllTextAsync(record, "{ not a record", Cancel);
        }
        else
        {
            File.Delete(record);
        }

        var answer = await AskAsync("POST", report.Id, "scenario.zip");

        if (refusal is null)
        {
            answer.Status.ShouldBe(200);
        }
        else
        {
            (answer.Status, Read(answer).GetProperty("error").GetString()).ShouldBe((409, refusal));
        }
    }

    [Fact]
    public async Task A_host_that_keeps_no_sessions_reads_no_report_s_table_as_over()
    {
        var report = await ReportedAsync(new FeedbackContext { Table = "20261116-110000-deadbeef", Seat = "player2", Round = 2 });
        var (_, tab) = TableHost.CuttingTabs(_scenarios, null, _hosted.Composer, FeedbackDesk.Open(new FeedbackIndex(_feedback, _feedback, _clock), new FeedbackMutes(null), _writes, _clock), new OperatorNotes(_actions), _clock);

        var sheet = Read(await tab!.HandleAsync("GET", $"{FeedbackAdminApi.Route}/{report.Id}", string.Empty, "mark"));

        (sheet.GetProperty("underWay").GetBoolean(), sheet.GetProperty("unread").GetBoolean()).ShouldBe((false, true));
        Actions(sheet).ShouldNotContain("diagnose");
    }

    [Fact]
    public async Task A_download_is_asked_for_with_a_post_and_never_kept_by_a_cache()
    {
        var (report, _, _, _) = await ReproducedAsync();

        var asGet = await AskAsync("GET", report.Id, "scenario.zip");
        var refused = await AskAsync("POST", Feedback.NewId(_clock.GetUtcNow()), "scenario.zip");

        asGet.Status.ShouldBe(404);
        refused.Headers.ShouldNotBeNull().ShouldContain(new KeyValuePair<string, string>("Cache-Control", "no-store, private"));
        (await ActivityAsync()).ShouldNotContain(line => line.StartsWith("feedback.download", StringComparison.Ordinal));
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
    public async Task The_list_keeps_the_open_reports_that_name_an_issue_whatever_their_status()
    {
        var filed = await ReportedAsync(new FeedbackContext { Table = "20261116-120000-deadbeef", Seat = "player1", Round = 2 });
        await ReportedAsync(new FeedbackContext { Table = "20261116-120000-deadbeef", Seat = "player1", Round = 3 });
        await AskAsync("PUT", filed.Id, "status", new { status = "filed", issue = 731 });
        await AskAsync("PUT", filed.Id, "status", new { status = "reproduced" });

        var list = Read(await Tab().HandleAsync("GET", FeedbackAdminApi.Route, string.Empty, "mark", "kind=bug&status=open&filed=true"));

        var row = list.GetProperty("feedback").EnumerateArray().ShouldHaveSingleItem();
        (row.GetProperty("id").GetString(), row.GetProperty("status").GetString(), row.GetProperty("issue").GetInt32()).ShouldBe((filed.Id, "reproduced", 731));
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

    /// <summary>
    /// The seat's journal length when its player wrote is counted in the entries the cut counts (#750): a report sent
    /// from round 2 of a played table, after the seat's own answer and before the other seat conceded, keeps in its
    /// download the one and not the other. People sit in both seats from round 2, so nothing else writes to the journal.
    /// </summary>
    [Fact]
    public async Task A_report_sent_from_a_seat_downloads_with_the_entries_its_round_held_when_it_was_sent()
    {
        using var registry = _hosted.Registry();
        var table = await _hosted.Composer.ComposeAsync(new TableRequest(null, null, Who: "mk", Handover: 2, Seed: 7), Cancel);
        using (var place = (await registry.ReserveAsync(Cancel)).ShouldNotBeNull())
        {
            await registry.AddAsync(table, place, Cancel);
        }

        var seat = table.Seats[0];
        (await table.Api.HandleAsync("GET", $"/api/seat/{table.Seats[1].Name}", string.Empty, table.Seats[1].Token)).Status.ShouldBe(200);
        await SpeedAsync(table, await AskedInRoundTwoAsync(table));
        var sent = await new FeedbackApi(new SendFeedback(_feedback, new GuestAddresses(), _writes, _clock), new AskFeedbackMuted(new FeedbackMutes(null), _clock), registry)
            .HandleAsync("POST", JsonSerializer.Serialize(new { kind = "bug", category = "rules", happened = "The bleed did not tick after the stun.", attach = true }), seat.Token, new AccountRequest(null, Secure: false), "203.0.113.7", "Firefox/140");
        var id = Read(sent).GetProperty("id").GetString()!;
        (await table.Api.HandleAsync("POST", $"/api/seat/{table.Seats[1].Name}/concede", string.Empty, table.Seats[1].Token)).Status.ShouldBeLessThan(300);
        await FinishedAsync(table);
        var cut = (await _scenarios.GetAsync(Read(await AskAsync("POST", id, "reproduce")).GetProperty("reproduction").GetProperty("id").GetString()!, Cancel)).ShouldNotBeNull();

        var answer = await AskAsync("POST", id, "scenario.zip");

        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        var after = ScenarioFile.Read(Unzipped(answer)).Scenario.After;
        (await _feedback.FindAsync(id, Cancel)).ShouldNotBeNull().Context.ShouldNotBeNull().JournalLength.ShouldBe(cut.Scenario.Cut.CutAt + after.Count);
        after.OfType<DecisionEntry>().ShouldHaveSingleItem().Decision.Slot.ShouldBe(PlayerSlot.Player1);
        after.Count.ShouldBe(cut.Scenario.After.Count - 1, "the other seat conceded after the report");
        cut.Scenario.After[^1].ShouldBe(new ConcessionEntry(PlayerSlot.Player2));
    }

    /// <summary>The first seat's view once the understudies have played round 1 and it is asked in round 2.</summary>
    private static async Task<JsonElement> AskedInRoundTwoAsync(PlayedTable table)
    {
        var seat = table.Seats[0];
        var view = default(JsonElement);
        await Waits.UntilAsync(
            async () =>
            {
                view = Read(await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token));
                return view.GetProperty("board").GetProperty("roundNumber").GetInt32() == 2 && view.TryGetProperty("waitingAsked", out var asked) && asked.ValueKind == JsonValueKind.Number;
            },
            "the first seat was asked in round 2",
            Cancel);
        return view;
    }

    /// <summary>The first seat's speed for its first creature still without one, journaled before it answers.</summary>
    private static async Task SpeedAsync(PlayedTable table, JsonElement view)
    {
        var seat = table.Seats[0];
        var creature = view.GetProperty("options").GetProperty("speed").GetProperty("missing")[0].GetInt32();
        var decision = JsonSerializer.Serialize(new { asked = view.GetProperty("waitingAsked").GetInt64(), kind = "Speed", creature, speed = "Quick" });
        var answer = await table.Api.HandleAsync("POST", $"/api/seat/{seat.Name}/decision", decision, seat.Token);
        answer.Status.ShouldBe(204, Encoding.UTF8.GetString(answer.Body));
    }

    private static async Task FinishedAsync(PlayedTable table)
    {
        await table.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), Cancel);
        await Waits.UntilAsync(() => table.IsFinished, "the table finished", Cancel);

        await table.RecordAsync(Cancel);
    }

    /// <summary>
    /// A reproduced report whose table's record also holds a seat's code and invitation and a signed-in seat, whose
    /// player is named: what each of the words the host gathers for the check is, by what the refusal calls it.
    /// </summary>
    private async Task<(Feedback Report, ScenarioDocument Cut, Dictionary<string, string> Held)> HeldByItsTableAsync()
    {
        var (report, table, cut, _) = await ReproducedAsync();
        var record = (await _hosted.Stored!.RecordAsync(table, Cancel)).ShouldNotBeNull();
        // Keys that do not begin with the table's id, as TableKeys draws them: the check finds each by itself, not by the table in it.
        RecordedSeat[] seats = [record.Seats[0] with { Code = "QX7KM2", Invitation = "inv-3b9c1f2e7a", Account = AccountId.New().ToString() }, record.Seats[1] with { Token = "seat-key-5d0e8a41c7" }];
        var file = Path.Combine(_hosted.RunsDirectory, table, TableRecord.File);
        File.Exists(file).ShouldBeTrue();
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(record with { Seats = seats, PilotToken = "pilot-key-9b2f6c03e1" }, ArtifactJson.DocumentOptions), Cancel);
        _hosted.Composer.NameOf = _ => Task.FromResult<string?>("Ysoldequill");
        return (report, cut, new Dictionary<string, string>
        {
            ["a seat's key"] = "seat-key-5d0e8a41c7",
            ["a seat's code"] = "QX7KM2",
            ["a seat's invitation"] = "inv-3b9c1f2e7a",
            ["the pilot's key"] = "pilot-key-9b2f6c03e1",
            ["a player's name"] = "Ysoldequill",
        });
    }

    [Theory]
    [InlineData("a seat's key")]
    [InlineData("a seat's code")]
    [InlineData("a seat's invitation")]
    [InlineData("the pilot's key")]
    [InlineData("a player's name")]
    public async Task A_scenario_that_still_holds_what_its_table_holds_is_not_offered(string what)
    {
        var (report, cut, held) = await HeldByItsTableAsync();
        var named = await _scenarios.PutAsync(cut with { Title = $"Sent by {held[what]}" }, Cancel);
        await _feedback.SaveAsync(report with { Reproduction = named }, Cancel);

        var refused = await AskAsync("POST", report.Id, "scenario.zip");

        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((409, "Feedback.NotRedacted"));
        Read(refused).GetProperty("message").GetString().ShouldBe($"The scenario still holds {what}, in its title: it is not offered.");
    }

    [Fact]
    public async Task A_practice_s_report_downloads_with_every_entry_it_copied()
    {
        var (_, _, cut, practised) = await ReproducedAsync();
        var copied = ScenarioFile.JournalLinesOf(cut.Scenario.After);
        // A practice's journal begins at its own cut: the length its seat read is no measure of the source's.
        var report = await ReportedAsync(new FeedbackContext { Scenario = practised, Seat = "player2", Round = 2, JournalLength = 1, PracticeEntries = copied });
        (await AskAsync("POST", report.Id, "reproduce")).Status.ShouldBe(200);

        var answer = await AskAsync("POST", report.Id, "scenario.zip");

        answer.Status.ShouldBe(200, Encoding.UTF8.GetString(answer.Body));
        copied.Count.ShouldBeGreaterThan(2);
        ScenarioFile.Read(Unzipped(answer)).Scenario.After.Count.ShouldBe(copied.Count);
    }
}
