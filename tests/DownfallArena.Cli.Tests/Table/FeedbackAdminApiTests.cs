using System.Text;
using System.Text.Json;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Admin;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.Infrastructure.Admin;
using DownfallArena.Infrastructure.FeedbackBox;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The Feedback tab's routes (docs/tabletop/feedback-plan.md, R3), behind the admin panel's gate: the list, a sheet with
/// its position, the status by hand, a kind moved both ways, a sender muted and let through, each an Activity line.
/// </summary>
public sealed class FeedbackAdminApiTests : IDisposable
{
    private const string Token = "operator-token";
    private const string Address = "203.0.113.7";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"feedback-admin-{Guid.CreateVersion7():N}");
    private readonly HostedTables _hosted = new(recording: true);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly TableRegistry _registry;
    private readonly FileFeedbackStore _feedback;
    private readonly FeedbackIndex _index;
    private readonly FeedbackWrites _writes = new();
    private readonly FeedbackMutes _mutes = new(null);
    private readonly OperatorActions _actions;

    public FeedbackAdminApiTests()
    {
        _registry = _hosted.Registry();
        _feedback = new FileFeedbackStore(Path.Combine(_root, "feedback"));
        _index = new FeedbackIndex(_feedback, _feedback, _clock);
        _actions = new OperatorActions(new FileOperatorLog(Path.Combine(_root, "log")), _clock);
    }

    public void Dispose()
    {
        _writes.Dispose();
        _registry.Dispose();
        _hosted.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonElement Read(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private static readonly AccountRequest SignedOut = new(null, Secure: false);

    private AdminApi Admin() =>
        new(_registry, _hosted.Composer, OperatorGate.WithToken(Token), _clock, "Rules", _hosted.Stored)
        {
            Feedback = new FeedbackAdminApi(FeedbackDesk.Open(_index, _mutes, _writes, _clock), _hosted.Stored, _clock) { Notes = new OperatorNotes(_actions) },
        };

    /// <summary>The players' route over the index's store, as the host wires it (#866).</summary>
    private FeedbackApi Players() => new(new SendFeedback(_index.Records, new GuestAddresses(), _writes, _clock, _mutes), new AskFeedbackMuted(_mutes, _clock), _registry);

    /// <summary>Asks as the operator, then moves the clock a second, so the Activity lines sort as they were noted.</summary>
    private async Task<StudioResponse> AskAsync(string method, string path, object? body = null, string? query = null, string? token = Token)
    {
        var answer = await Admin().HandleAsync(method, path, body is null ? string.Empty : JsonSerializer.Serialize(body), token, principal: null, query: query);
        _clock.Advance(TimeSpan.FromSeconds(1));
        return answer;
    }

    private static string Report(string happened = "The bleed did not tick after the stun.") =>
        JsonSerializer.Serialize(new { kind = "bug", category = "rules", happened, expected = "One damage in the upkeep.", attach = true, page = "/battle" });

    private static string Idea() =>
        JsonSerializer.Serialize(new { kind = "idea", where = "battle", problem = "I wanted to see the last round again and could not.", page = "/battle" });

    /// <summary>A table with a person in seat 1 that has reached it, so the match is under way and waits on that person.</summary>
    private async Task<PlayedTable> BegunAsync()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, Cancel);
        using var place = (await _registry.ReserveAsync(Cancel)).ShouldNotBeNull();
        await _registry.AddAsync(table, place, Cancel);
        var seat = table.Seats[0];
        (await table.Api.HandleAsync("GET", $"/api/seat/{seat.Name}", string.Empty, seat.Token)).Status.ShouldBe(200);
        return table;
    }

    /// <summary>Sends from the seat, then moves the clock past the two minutes between two pieces of feedback.</summary>
    private async Task<string> SentAsync(PlayedTable table, string body)
    {
        var answer = await Players().HandleAsync("POST", body, table.Seats[0].Token, SignedOut, Address, "Firefox/140");
        answer.Status.ShouldBe(201, Encoding.UTF8.GetString(answer.Body));
        _clock.Advance(SendFeedback.Apart);
        return Read(answer).GetProperty("id").GetString()!;
    }

    private async Task<string[]> ActivityAsync() =>
        [.. (await _actions.PageAsync(1, Cancel)).Actions.Select(action => $"{action.Action} {action.Target} {action.Detail}".TrimEnd())];

    [Fact]
    public async Task The_tab_is_the_operator_s_alone()
    {
        (await AskAsync("GET", FeedbackAdminApi.Route, token: null)).Status.ShouldBe(403);
        (await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{Feedback.NewId(_clock.GetUtcNow())}/status", new { status = "seen" }, token: "someone-else")).Status.ShouldBe(403);
    }

    [Fact]
    public async Task A_report_s_sheet_holds_its_words_its_position_and_its_table_still_under_way()
    {
        var table = await BegunAsync();
        var id = await SentAsync(table, Report());

        var sheet = Read(await AskAsync("GET", $"{FeedbackAdminApi.Route}/{id}"));

        (sheet.GetProperty("kind").GetString(), sheet.GetProperty("status").GetString(), sheet.GetProperty("text").GetString()).ShouldBe(("bug", "new", "The bleed did not tick after the stun."));
        (sheet.GetProperty("guest").GetBoolean(), sheet.GetProperty("underWay").GetBoolean()).ShouldBe((true, true));
        sheet.GetProperty("session").GetString().ShouldBe($"/session/{table.Id}");
        var context = sheet.GetProperty("context");
        (context.GetProperty("table").GetString(), context.GetProperty("round").GetInt32()).ShouldBe((table.Id, 1));
        context.GetProperty("position").GetProperty("slot").GetString().ShouldBe("Player1");
        sheet.GetProperty("mute").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_report_sent_and_marked_through_the_routes_is_placed_in_the_index_as_it_is_kept()
    {
        var id = await SentAsync(await BegunAsync(), Report());

        await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/status", new { status = "seen" });

        var row = (await _feedback.ReadIndexAsync(Cancel)).Content.ShouldNotBeNull().Rows.ShouldHaveSingleItem();
        (row.Id, row.Status, row.Kind).ShouldBe((id, FeedbackStatus.Seen, FeedbackKind.Bug));
    }

    [Fact]
    public async Task The_list_is_filtered_by_kind_and_status_newest_first()
    {
        var table = await BegunAsync();
        var report = await SentAsync(table, Report());
        var idea = await SentAsync(table, Idea());

        var all = Read(await AskAsync("GET", FeedbackAdminApi.Route)).GetProperty("feedback");
        all.EnumerateArray().Select(row => row.GetProperty("id").GetString()).ShouldBe([idea, report]);
        var ideas = Read(await AskAsync("GET", FeedbackAdminApi.Route, query: "kind=idea")).GetProperty("feedback");
        var row = ideas.EnumerateArray().ShouldHaveSingleItem();
        (row.GetProperty("kind").GetString(), row.GetProperty("category").GetString(), row.GetProperty("line").GetString()).ShouldBe(("idea", "battle", "I wanted to see the last round again and could not."));

        await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{report}/status", new { status = "seen" });
        Read(await AskAsync("GET", FeedbackAdminApi.Route, query: "status=new")).GetProperty("feedback").EnumerateArray().Select(one => one.GetProperty("id").GetString()).ShouldBe([idea]);
    }

    [Fact]
    public async Task A_kind_changed_both_ways_keeps_its_words_and_is_noted_each_time()
    {
        var table = await BegunAsync();
        var id = await SentAsync(table, Report());

        var idea = Read(await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/kind", new { kind = "idea" }));
        (idea.GetProperty("kind").GetString(), idea.GetProperty("status").GetString(), idea.GetProperty("text").GetString()).ShouldBe(("idea", "seen", "The bleed did not tick after the stun."));
        var bug = Read(await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/kind", new { kind = "bug" }));
        (bug.GetProperty("kind").GetString(), bug.GetProperty("expected").GetString()).ShouldBe(("bug", "One damage in the upkeep."));
        bug.GetProperty("context").GetProperty("position").GetProperty("slot").GetString().ShouldBe("Player1");

        (await ActivityAsync()).ShouldBe([$"feedback.convert {id} idea → bug", $"feedback.convert {id} bug → idea"]);
    }

    [Fact]
    public async Task A_status_is_seen_reproduced_or_dismissed_with_a_reason_and_noted()
    {
        var table = await BegunAsync();
        var id = await SentAsync(table, Report());

        (await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/status", new { status = "shipped" })).Status.ShouldBe(400);
        (await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/status", new { status = "dismissed" })).Status.ShouldBe(400);
        Read(await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/status", new { status = "reproduced" })).GetProperty("status").GetString().ShouldBe("reproduced");
        var dismissed = Read(await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/status", new { status = "dismissed", reason = "Works as the rules say." }));

        (dismissed.GetProperty("status").GetString(), dismissed.GetProperty("reason").GetString()).ShouldBe(("dismissed", "Works as the rules say."));
        (await ActivityAsync()).ShouldBe([$"feedback.status {id} dismissed: Works as the rules say.", $"feedback.status {id} reproduced"]);
        (await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/kind", new { kind = "idea" })).Status.ShouldBe(409);
    }

    [Fact]
    public async Task A_muted_sender_is_refused_with_feedback_muted_and_let_through_once_lifted()
    {
        var table = await BegunAsync();
        var id = await SentAsync(table, Report());

        var muted = Read(await AskAsync("POST", $"{FeedbackAdminApi.Route}/{id}/mute"));
        muted.GetProperty("mute").GetProperty("why").GetString().ShouldBe(FeedbackMute.ByOperator);
        var refused = await Players().HandleAsync("POST", Idea(), table.Seats[0].Token, SignedOut, Address, null);
        (refused.Status, Read(refused).GetProperty("error").GetString()).ShouldBe((403, "Feedback.Muted"));

        Read(await AskAsync("DELETE", $"{FeedbackAdminApi.Route}/{id}/mute")).GetProperty("mute").ValueKind.ShouldBe(JsonValueKind.Null);
        (await Players().HandleAsync("POST", Idea(), table.Seats[0].Token, SignedOut, Address, null)).Status.ShouldBe(201);
        (await ActivityAsync()).ShouldBe([$"feedback.unmute {id}", $"feedback.mute {id} 30 days"]);
    }

    [Fact]
    public async Task A_page_asking_before_its_player_writes_is_told_the_seat_is_muted_and_let_through_once_lifted()
    {
        var table = await BegunAsync();
        var id = await SentAsync(table, Report());
        (await Players().HandleAsync("GET", string.Empty, table.Seats[0].Token, SignedOut, Address, null)).Status.ShouldBe(204);

        await AskAsync("POST", $"{FeedbackAdminApi.Route}/{id}/mute");
        var asked = await Players().HandleAsync("GET", string.Empty, table.Seats[0].Token, SignedOut, Address, null);

        (asked.Status, Read(asked).GetProperty("error").GetString()).ShouldBe((403, "Feedback.Muted"));
        (await Players().HandleAsync("GET", string.Empty, table.Seats[1].Token, SignedOut, Address, null)).Status.ShouldBe(204);
        await AskAsync("DELETE", $"{FeedbackAdminApi.Route}/{id}/mute");
        (await Players().HandleAsync("GET", string.Empty, table.Seats[0].Token, SignedOut, Address, null)).Status.ShouldBe(204);
    }

    [Fact]
    public async Task Three_marked_as_spam_in_thirty_days_mute_their_sender()
    {
        var table = await BegunAsync();
        var ids = new List<string>();
        foreach (var round in new[] { "first", "second", "third" })
        {
            ids.Add(await SentAsync(table, Idea().Replace("again", $"again, the {round} time", StringComparison.Ordinal)));
            _clock.Advance(SendFeedback.IdeaMerged);
        }

        foreach (var id in ids)
        {
            (await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/status", new { status = "dismissed", reason = Feedback.SpamReason })).Status.ShouldBe(200);
        }

        var sheet = Read(await AskAsync("GET", $"{FeedbackAdminApi.Route}/{ids[0]}"));
        sheet.GetProperty("mute").GetProperty("why").GetString().ShouldBe(FeedbackMute.BySpam);
        (await Players().HandleAsync("POST", Report(), table.Seats[0].Token, SignedOut, Address, null)).Status.ShouldBe(403);
        // The mark and the mute it earned are noted in one request, at one instant: the mute, noted second, reads on top (#674).
        (await ActivityAsync()).ShouldBe([$"feedback.mute {ids[2]} 3 marked as spam in 30 days", $"feedback.spam {ids[2]}", $"feedback.spam {ids[1]}", $"feedback.spam {ids[0]}"]);
    }

    [Fact]
    public async Task A_mute_ends_after_thirty_days()
    {
        var table = await BegunAsync();
        var id = await SentAsync(table, Report());
        await AskAsync("POST", $"{FeedbackAdminApi.Route}/{id}/mute");

        _clock.Advance(FeedbackMute.Lasts);

        (await Players().HandleAsync("POST", Idea(), table.Seats[0].Token, SignedOut, Address, null)).Status.ShouldBe(201);
        Read(await AskAsync("GET", $"{FeedbackAdminApi.Route}/{id}")).GetProperty("mute").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Feedback_not_kept_and_a_route_not_offered_answer_404()
    {
        var unknown = Feedback.NewId(_clock.GetUtcNow());

        Read(await AskAsync("GET", $"{FeedbackAdminApi.Route}/{unknown}")).GetProperty("error").GetString().ShouldBe("Feedback.NotFound");
        (await AskAsync("POST", $"{FeedbackAdminApi.Route}/{unknown}/mute")).Status.ShouldBe(404);
        (await AskAsync("PATCH", FeedbackAdminApi.Route)).Status.ShouldBe(404);
    }

    [Fact]
    public async Task A_filter_that_names_nothing_the_host_knows_is_refused()
    {
        (await AskAsync("GET", FeedbackAdminApi.Route, query: "status=closed")).Status.ShouldBe(400);
        (await AskAsync("GET", FeedbackAdminApi.Route, query: "kind=bugs")).Status.ShouldBe(400);
        (await AskAsync("GET", FeedbackAdminApi.Route, query: "category=weather")).Status.ShouldBe(400);
        (await AskAsync("GET", FeedbackAdminApi.Route, query: "kind=bug&status=open&category=looks")).Status.ShouldBe(200);
    }

    [Fact]
    public async Task A_spam_mark_asked_again_is_noted_once()
    {
        var table = await BegunAsync();
        var id = await SentAsync(table, Report());

        await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/status", new { status = "dismissed", reason = Feedback.SpamReason });
        var again = Read(await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/status", new { status = "dismissed", reason = Feedback.SpamReason }));

        again.GetProperty("spam").GetBoolean().ShouldBeTrue();
        (await ActivityAsync()).ShouldBe([$"feedback.spam {id}"]);
    }

    [Fact]
    public async Task The_sheet_says_which_of_its_actions_apply()
    {
        var table = await BegunAsync();
        var id = await SentAsync(table, Report());

        Actions(Read(await AskAsync("GET", $"{FeedbackAdminApi.Route}/{id}"))).ShouldBe(["seen", "reproduced", "dismiss", "spam", "toIdea", "file", "diagnose", "mute"]);
        Actions(Read(await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{id}/status", new { status = "dismissed", reason = "A duplicate." }))).ShouldBe(["seen", "reproduced", "diagnose", "mute"]);
        Actions(Read(await AskAsync("POST", $"{FeedbackAdminApi.Route}/{id}/mute"))).ShouldBe(["seen", "reproduced", "diagnose", "unmute"]);
    }

    [Fact]
    public async Task A_signed_in_sender_is_muted_in_its_activity_record_and_noted_once()
    {
        var activity = new FileActivityStore(Path.Combine(_root, "accounts"));
        var account = AccountId.New();
        var id = Feedback.NewId(_clock.GetUtcNow());
        await _feedback.SaveAsync(new Feedback { Id = id, Kind = FeedbackKind.Idea, At = _clock.GetUtcNow(), Sender = account.Value, Account = account.Value, Category = "lobby", Text = "Invite a friend by name." }, Cancel);
        var admin = new AdminApi(_registry, _hosted.Composer, OperatorGate.WithToken(Token), _clock, "Rules", _hosted.Stored)
        {
            Feedback = new FeedbackAdminApi(FeedbackDesk.Open(new FeedbackIndex(_feedback, _feedback, _clock), new FeedbackMutes(new AccountActivityLog(activity, _clock)), _writes, _clock), _hosted.Stored, _clock) { Notes = new OperatorNotes(_actions) },
        };

        (await admin.HandleAsync("POST", $"{FeedbackAdminApi.Route}/{id}/mute", string.Empty, Token, principal: null)).Status.ShouldBe(200);
        (await admin.HandleAsync("POST", $"{FeedbackAdminApi.Route}/{id}/mute", string.Empty, Token, principal: null)).Status.ShouldBe(200);

        (await activity.FindAsync(account, Cancel)).ShouldNotBeNull().FeedbackMute.ShouldNotBeNull().Why.ShouldBe(FeedbackMute.ByOperator);
        (await ActivityAsync()).ShouldBe([$"feedback.mute {id} 30 days"]);
    }

    [Fact]
    public async Task An_idea_merged_into_another_keeps_its_sender_and_is_read_inside_it()
    {
        var first = await SentAsync(await BegunAsync(), Idea());
        var second = await SentAsync(await BegunAsync(), Idea().Replace("could not", "found no way", StringComparison.Ordinal));

        var merged = Read(await AskAsync("POST", $"{FeedbackAdminApi.Route}/{second}/merge", new { into = first }));

        merged.GetProperty("mergedInto").GetProperty("id").GetString().ShouldBe(first);
        Actions(merged).ShouldBe(["unmerge", "dismiss", "spam", "mute"]);
        merged.GetProperty("askedBy").ValueKind.ShouldBe(JsonValueKind.Null, "a merged idea is counted in the one it joined");
        var row = Read(await AskAsync("GET", FeedbackAdminApi.Route, query: "kind=idea")).GetProperty("feedback").EnumerateArray().ShouldHaveSingleItem();
        (row.GetProperty("id").GetString(), row.GetProperty("askedBy").GetInt32()).ShouldBe((first, 2));
        var sheet = Read(await AskAsync("GET", $"{FeedbackAdminApi.Route}/{first}"));
        sheet.GetProperty("askedBy").GetInt32().ShouldBe(2);
        var ask = sheet.GetProperty("asks").EnumerateArray().ShouldHaveSingleItem();
        (ask.GetProperty("id").GetString(), ask.GetProperty("text").GetString(), ask.GetProperty("guest").GetBoolean()).ShouldBe((second, "I wanted to see the last round again and found no way.", true));
        Actions(sheet).ShouldBe(["seen", "planned", "shipped", "dismiss", "spam", "declined", "merge", "prompt", "diagnose", "mute"]);
        (await ActivityAsync()).ShouldBe([$"feedback.merge {second} into {first}"]);
    }

    [Fact]
    public async Task An_idea_unmerged_is_listed_again_as_its_own_and_unmerged_again_is_noted_once()
    {
        var first = await SentAsync(await BegunAsync(), Idea());
        var second = await SentAsync(await BegunAsync(), Idea().Replace("could not", "found no way", StringComparison.Ordinal));
        await AskAsync("POST", $"{FeedbackAdminApi.Route}/{second}/merge", new { into = first });

        var unmerged = Read(await AskAsync("DELETE", $"{FeedbackAdminApi.Route}/{second}/merge"));
        var again = await AskAsync("DELETE", $"{FeedbackAdminApi.Route}/{second}/merge");

        (unmerged.GetProperty("mergedInto").ValueKind, unmerged.GetProperty("askedBy").GetInt32()).ShouldBe((JsonValueKind.Null, 1));
        Actions(unmerged).ShouldContain("merge");
        Actions(unmerged).ShouldNotContain("unmerge");
        again.Status.ShouldBe(200);
        var rows = Read(await AskAsync("GET", FeedbackAdminApi.Route, query: "kind=idea")).GetProperty("feedback").EnumerateArray()
            .Select(row => (row.GetProperty("id").GetString(), row.GetProperty("askedBy").GetInt32())).ToList();
        rows.ShouldBe([(second, 1), (first, 1)]);
        (await AskAsync("DELETE", $"{FeedbackAdminApi.Route}/{Feedback.NewId(_clock.GetUtcNow())}/merge")).Status.ShouldBe(404);
        (await ActivityAsync()).ShouldBe([$"feedback.unmerge {second} from {first}", $"feedback.merge {second} into {first}"]);
    }

    [Fact]
    public async Task A_merge_into_a_bug_report_or_into_itself_is_refused_and_merged_again_is_noted_once()
    {
        var table = await BegunAsync();
        var report = await SentAsync(table, Report());
        var idea = await SentAsync(table, Idea());
        var other = await SentAsync(await BegunAsync(), Idea());

        (await AskAsync("POST", $"{FeedbackAdminApi.Route}/{idea}/merge", new { into = report })).Status.ShouldBe(400);
        (await AskAsync("POST", $"{FeedbackAdminApi.Route}/{idea}/merge", new { into = idea })).Status.ShouldBe(400);
        (await AskAsync("POST", $"{FeedbackAdminApi.Route}/{idea}/merge", new { })).Status.ShouldBe(400);
        (await AskAsync("POST", $"{FeedbackAdminApi.Route}/{idea}/merge", new { into = other })).Status.ShouldBe(200);
        (await AskAsync("POST", $"{FeedbackAdminApi.Route}/{idea}/merge", new { into = other })).Status.ShouldBe(200);
        (await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{idea}/status", new { status = "seen" })).Status.ShouldBe(409);
        (await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{other}/kind", new { kind = "bug" })).Status.ShouldBe(409);

        (await ActivityAsync()).ShouldBe([$"feedback.merge {idea} into {other}"]);
    }

    [Fact]
    public async Task An_idea_is_planned_with_its_plan_shipped_or_declined_with_a_reason_and_noted()
    {
        var table = await BegunAsync();
        var idea = await SentAsync(table, Idea());

        (await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{idea}/status", new { status = "planned", plan = "README.md" })).Status.ShouldBe(400);
        var planned = Read(await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{idea}/status", new { status = "planned", plan = "docs/tabletop/replay-plan.md" }));
        (planned.GetProperty("status").GetString(), planned.GetProperty("plan").GetString()).ShouldBe(("planned", "docs/tabletop/replay-plan.md"));
        (await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{idea}/status", new { status = "declined" })).Status.ShouldBe(400);
        var declined = Read(await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{idea}/status", new { status = "declined", reason = "Not for this game." }));
        (declined.GetProperty("status").GetString(), declined.GetProperty("reason").GetString()).ShouldBe(("declined", "Not for this game."));
        var shipped = Read(await AskAsync("PUT", $"{FeedbackAdminApi.Route}/{idea}/status", new { status = "shipped" }));
        (shipped.GetProperty("status").GetString(), shipped.GetProperty("plan").GetString()).ShouldBe(("shipped", "docs/tabletop/replay-plan.md"));

        (await ActivityAsync()).ShouldBe([
            $"feedback.status {idea} shipped",
            $"feedback.status {idea} declined: Not for this game.",
            $"feedback.status {idea} planned: docs/tabletop/replay-plan.md",
        ]);
        Read(await AskAsync("GET", FeedbackAdminApi.Route, query: "kind=idea&status=shipped")).GetProperty("total").GetInt32().ShouldBe(1);
    }

    private static string[] Actions(JsonElement sheet) => [.. sheet.GetProperty("actions").EnumerateArray().Select(action => action.GetString()!)];
}
