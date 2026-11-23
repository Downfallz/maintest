using System.Text;
using System.Text.Json;
using DownfallArena.Application.Work;
using DownfallArena.Cli.Studio;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>The notices in the operator's Jobs tab (notifications plan, N6): the queues' lengths and the worker's counts.</summary>
public sealed class NoticesApiTests
{
    private readonly InMemoryWorkChannel _notices = new(QueuedNotice.Queue, TimeProvider.System);
    private readonly InMemoryWorkChannel _poison = new(PoisonedWork.QueueOf(QueuedNotice.Queue), TimeProvider.System);
    private readonly InMemoryWorkChannel _letters = new(DeadLetter.QueueOf(QueuedNotice.Queue), TimeProvider.System);
    private readonly NoticeCounts _counts = new(new InMemoryNoticeCountStore(), new Fixed());

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private NoticesApi Api(NoticeCounts? counts) => new(new TurnQueues(_notices, _poison, _letters), counts);

    private static JsonElement Json(StudioResponse response) => JsonDocument.Parse(Encoding.UTF8.GetString(response.Body)).RootElement.Clone();

    [Fact]
    public async Task The_three_queues_are_listed_with_their_lengths()
    {
        await _notices.SendAsync("{}", cancellationToken: Cancel);
        await _letters.SendAsync("{}", cancellationToken: Cancel);

        var listed = Json(await Api(_counts).HandleAsync("GET", NoticesApi.Prefix));

        listed.GetProperty("queues").EnumerateArray().Select(queue => (queue.GetProperty("name").GetString(), queue.GetProperty("count").GetInt32()))
            .ShouldBe([(QueuedNotice.Queue, 1), (_poison.Name, 0), (_letters.Name, 1)]);
    }

    [Fact]
    public async Task What_the_worker_counted_is_answered_a_kind_each_for_today_and_the_week()
    {
        await _counts.AddAsync([new("turn", 3, 1, 0), new("match", 1, 0, 2)], Cancel);

        var listed = Json(await Api(_counts).HandleAsync("GET", NoticesApi.Prefix));

        listed.GetProperty("counted").GetBoolean().ShouldBeTrue();
        listed.GetProperty("days").GetInt32().ShouldBe(NoticeCounts.Days);
        listed.GetProperty("today").GetString().ShouldBe("2026-11-23");
        foreach (var span in new[] { "day", "week" })
        {
            listed.GetProperty(span).EnumerateArray()
                .Select(kind => (kind.GetProperty("kind").GetString(), kind.GetProperty("sent").GetInt64(), kind.GetProperty("refused").GetInt64(), kind.GetProperty("gone").GetInt64()))
                .ShouldBe([("match", 1, 0, 2), ("turn", 3, 1, 0)]);
        }
    }

    [Fact]
    public async Task A_host_whose_counts_could_not_be_opened_says_it_counts_nothing_and_still_lists_its_queues()
    {
        var listed = Json(await Api(counts: null).HandleAsync("GET", NoticesApi.Prefix));

        listed.GetProperty("counted").GetBoolean().ShouldBeFalse();
        listed.GetProperty("day").GetArrayLength().ShouldBe(0);
        listed.GetProperty("queues").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task Counts_that_cannot_be_read_are_answered_unavailable_without_the_store_s_text()
    {
        var answer = await Api(new NoticeCounts(new Unreachable(), TimeProvider.System)).HandleAsync("GET", NoticesApi.Prefix);

        answer.Status.ShouldBe(503);
        Json(answer).GetProperty("error").GetString().ShouldBe("Notices.Unavailable");
        Encoding.UTF8.GetString(answer.Body).ShouldNotContain("secret-path");
    }

    [Theory]
    [InlineData("POST", NoticesApi.Prefix)]
    [InlineData("GET", NoticesApi.Prefix + "/elsewhere")]
    public async Task Any_other_route_under_it_is_not_found(string method, string path)
    {
        var answer = await Api(_counts).HandleAsync(method, path);

        answer.Status.ShouldBe(404);
        Json(answer).GetProperty("error").GetString().ShouldBe("Notices.NoRoute");
    }

    private sealed class Fixed : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 23, 23, 30, 0, TimeSpan.Zero);
    }

    private sealed class Unreachable : Application.Ports.INoticeCountStore
    {
        public Task<NoticeCountRead> ReadAsync(DateOnly day, CancellationToken cancellationToken = default) => throw new IOException("secret-path is out");

        public Task<bool> WriteAsync(DateOnly day, IReadOnlyList<NoticeCount> counts, string? version, CancellationToken cancellationToken = default) => throw new IOException("secret-path is out");

        public Task ForgetBeforeAsync(DateOnly day, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
