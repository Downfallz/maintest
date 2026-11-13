using System.Text;
using System.Text.Json;
using DownfallArena.Application.Tables.Tending;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
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
        var kept = (await _log.FindAsync((await _log.IdsAsync(Cancel)).ShouldHaveSingleItem(), Cancel)).ShouldNotBeNull();
        kept.Outcome.ShouldBe(TendOutcome.Failed);
        kept.By.ShouldBe("mk");
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
