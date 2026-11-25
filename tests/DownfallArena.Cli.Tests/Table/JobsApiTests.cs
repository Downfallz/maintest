using System.Text;
using System.Text.Json;
using DownfallArena.Application.Admin;
using DownfallArena.Application.Tables.Tending;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Admin;
using DownfallArena.Infrastructure.Tables;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>The operator's jobs (admin plan, A3): the tending runs as the panel reads them, and a pass run now.</summary>
public sealed class JobsApiTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 18, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"jobs-api-{Guid.CreateVersion7():N}");
    private readonly FileTendLog _log;

    public JobsApiTests()
    {
        _log = new FileTendLog(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    private JobsApi Jobs(Func<DateTimeOffset, Action<TableTend.Tended>, Task<TableTend.Tended>>? pass = null) =>
        new(_log, new FixedClock(Now), pass ?? ((_, _) => Task.FromResult(new TableTend.Tended(["never-began"], 2) { Warned = ["warned"] })));

    private static TendRun Run(DateTimeOffset started, string[] timedOut) =>
        new(TendRun.NewId(started), started, started.AddSeconds(1), TendTrigger.Schedule, null, TendOutcome.Completed, null, [], 0, timedOut, [], []);

    [Fact]
    public async Task The_runs_are_listed_newest_first_with_the_day_and_the_week_added_up()
    {
        await _log.RecordAsync(Run(Now.AddHours(-2), ["a"]), Cancel);
        await _log.RecordAsync(Run(Now.AddDays(-3), ["b", "c"]), Cancel);

        var page = Json(await Jobs().HandleAsync("GET", JobsApi.TendRoute, null, "op"));

        page.GetProperty("total").GetInt32().ShouldBe(2);
        page.GetProperty("runs")[0].GetProperty("trigger").GetString().ShouldBe("schedule");
        page.GetProperty("runs")[0].GetProperty("started").GetDateTimeOffset().ShouldBe(Now.AddHours(-2));
        page.GetProperty("day").GetProperty("timedOut").GetInt32().ShouldBe(1);
        page.GetProperty("week").GetProperty("timedOut").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task A_pass_run_now_is_kept_as_a_manual_run_by_the_operator()
    {
        var answer = await Jobs().HandleAsync("POST", JobsApi.TendRoute, null, "mk");

        answer.Status.ShouldBe(200);
        var run = Json(answer).GetProperty("run");
        run.GetProperty("trigger").GetString().ShouldBe("manual");
        run.GetProperty("by").GetString().ShouldBe("mk");
        run.GetProperty("outcome").GetString().ShouldBe("completed");
        run.GetProperty("neverBegan").EnumerateArray().Select(id => id.GetString()).ShouldBe(["never-began"]);
        (await _log.IdsAsync(Cancel)).ShouldHaveSingleItem().ShouldBe(run.GetProperty("id").GetString());
    }

    /// <summary>Run now removes the idle guests too (#1225): the answer, the kept run and the operator log say how many (#1249).</summary>
    [Fact]
    public async Task A_pass_run_now_says_how_many_guests_notices_and_feedback_it_let_go_of()
    {
        var operatorLog = new FileOperatorLog(Path.Combine(_root, "operator"));
        var jobs = Jobs((_, _) => Task.FromResult(new TableTend.Tended([], 0) { GuestsRemoved = 3, NoticesErased = 2, FeedbackLetGo = 1 }));
        jobs.Notes = new OperatorNotes(new OperatorActions(operatorLog, new FixedClock(Now)));

        var run = Json(await jobs.HandleAsync("POST", JobsApi.TendRoute, null, "mk")).GetProperty("run");

        (run.GetProperty("guestsRemoved").GetInt32(), run.GetProperty("noticesErased").GetInt32(), run.GetProperty("feedbackLetGo").GetInt32()).ShouldBe((3, 2, 1));
        var kept = (await _log.FindAsync(run.GetProperty("id").GetString()!, Cancel)).ShouldNotBeNull();
        (kept.GuestsRemoved, kept.NoticesErased, kept.FeedbackLetGo).ShouldBe((3, 2, 1));
        var noted = (await operatorLog.FindAsync((await operatorLog.IdsAsync(Cancel)).ShouldHaveSingleItem(), Cancel)).ShouldNotBeNull();
        noted.Detail.ShouldNotBeNull().ShouldContain("3 guests removed");
        noted.Detail.ShouldContain("2 notice logs erased");
        noted.Detail.ShouldContain("1 feedback let go of");
    }

    [Fact]
    public async Task A_run_kept_before_the_guests_were_counted_reads_as_none_removed()
    {
        var started = Now.AddHours(-1);
        var id = TendRun.NewId(started);
        var old = $$"""{"id":"{{id}}","started":"{{started:O}}","finished":"{{started.AddSeconds(1):O}}","trigger":"schedule","by":null,"outcome":"completed","error":null,"neverBegan":[],"invitationsExpired":0,"timedOut":[],"abandoned":[],"warned":[]}""";

        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, $"{id}.json"), old, Cancel);

        var sheet = Json(await Jobs().HandleAsync("GET", $"{JobsApi.TendRoute}/{id}", null, "op")).GetProperty("run");

        sheet.GetProperty("guestsRemoved").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task A_second_pass_asked_while_one_runs_is_told_so()
    {
        var held = new TaskCompletionSource<TableTend.Tended>(TaskCreationOptions.RunContinuationsAsynchronously);
        var jobs = Jobs((_, _) => held.Task);
        var first = jobs.HandleAsync("POST", JobsApi.TendRoute, null, "mk");

        var second = await jobs.HandleAsync("POST", JobsApi.TendRoute, null, "mk");
        held.SetResult(new TableTend.Tended([], 0));

        second.Status.ShouldBe(409);
        Json(second).GetProperty("error").GetString().ShouldBe("Jobs.Passing");
        (await first).Status.ShouldBe(200);
    }

    /// <summary>
    /// One pass at a time is each host's own: two of them in one process -- the test classes, which run side by
    /// side -- do not turn each other away (#666).
    /// </summary>
    [Fact]
    public async Task Two_hosts_in_one_process_each_run_their_own_pass()
    {
        var held = new TaskCompletionSource<TableTend.Tended>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Jobs((_, _) => held.Task).HandleAsync("POST", JobsApi.TendRoute, null, "mk");

        var second = await Jobs().HandleAsync("POST", JobsApi.TendRoute, null, "mk");
        held.SetResult(new TableTend.Tended([], 0));

        second.Status.ShouldBe(200);
        (await first).Status.ShouldBe(200);
    }

    [Fact]
    public async Task A_run_s_sheet_is_read_by_its_id()
    {
        var kept = Run(Now, ["late"]);
        await _log.RecordAsync(kept, Cancel);

        var sheet = Json(await Jobs().HandleAsync("GET", $"{JobsApi.TendRoute}/{kept.Id}", null, "op"));

        sheet.GetProperty("run").GetProperty("timedOut")[0].GetString().ShouldBe("late");
    }

    [Theory]
    [InlineData("20261113T180000000Z-abcdef")]
    [InlineData("..%2Faccounts%2Fsession.key")]
    public async Task A_run_nobody_kept_is_not_found(string id)
    {
        (await Jobs().HandleAsync("GET", $"{JobsApi.TendRoute}/{id}", null, "op")).Status.ShouldBe(404);
    }

    [Fact]
    public async Task A_pass_the_store_stops_is_said_and_kept_as_failed()
    {
        var answer = await Jobs((_, _) => throw new IOException("the store is out")).HandleAsync("POST", JobsApi.TendRoute, null, "mk");

        answer.Status.ShouldBe(503);
        var said = Encoding.UTF8.GetString(answer.Body);
        said.ShouldContain("Jobs.Failed");
        said.ShouldNotContain("the store is out", Case.Insensitive);
        var kept = (await _log.FindAsync((await _log.IdsAsync(Cancel)).ShouldHaveSingleItem(), Cancel)).ShouldNotBeNull();
        kept.Outcome.ShouldBe(TendOutcome.Failed);
        kept.By.ShouldBe("mk");
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
