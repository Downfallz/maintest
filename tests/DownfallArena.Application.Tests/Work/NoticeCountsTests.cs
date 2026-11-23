using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;

namespace DownfallArena.Application.Tests.Work;

/// <summary>
/// The notices worker's counts (notifications plan, N6): added to the day's, a kind each, over the version read so two
/// processes both keep theirs, and read as today and the last seven days.
/// </summary>
public sealed class NoticeCountsTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly SteppedClock _clock = new(new DateTimeOffset(2026, 11, 23, 20, 0, 0, TimeSpan.Zero));
    private readonly VersionedCounts _store = new();
    private readonly NoticeCounts _counts;

    public NoticeCountsTests()
    {
        _counts = new NoticeCounts(_store, _clock);
    }

    [Fact]
    public async Task Counts_are_added_to_the_days_a_kind_each()
    {
        await _counts.AddAsync([new("turn", 2, 0, 0), new("friend", 1, 1, 0)], Cancel);

        await _counts.AddAsync([new("turn", 1, 0, 1)], Cancel);

        var week = await _counts.WeekAsync(Cancel);
        week.Today.ShouldBe(new DateOnly(2026, 11, 23));
        week.Day.ShouldBe([new NoticeCount("friend", 1, 1, 0), new NoticeCount("turn", 3, 0, 1)]);
    }

    [Fact]
    public async Task Two_processes_that_count_at_once_both_keep_their_counts()
    {
        await _counts.AddAsync([new("turn", 1, 0, 0)], Cancel);
        _store.BeforeNextWrite = () => new NoticeCounts(_store, _clock).AddAsync([new("match", 1, 0, 0)], Cancel);

        (await _counts.AddAsync([new("turn", 1, 0, 0)], Cancel)).ShouldBeTrue();

        (await _counts.WeekAsync(Cancel)).Day.ShouldBe([new NoticeCount("match", 1, 0, 0), new NoticeCount("turn", 2, 0, 0)]);
    }

    [Fact]
    public async Task Counts_another_process_keeps_writing_over_are_given_up_and_said_so()
    {
        _store.RefuseEveryWrite = true;

        (await _counts.AddAsync([new("turn", 1, 0, 0)], Cancel)).ShouldBeFalse();

        _store.Writes.ShouldBe(NoticeCounts.MostTries);
    }

    [Fact]
    public async Task Nothing_counted_writes_nothing()
    {
        (await _counts.AddAsync([new("turn", 0, 0, 0)], Cancel)).ShouldBeTrue();

        _store.Writes.ShouldBe(0);
    }

    [Fact]
    public async Task The_week_is_the_seven_days_that_end_today_and_today_alone_is_the_day()
    {
        await _counts.AddAsync([new("turn", 1, 0, 0)], Cancel);
        _clock.Advance(TimeSpan.FromDays(6));
        await _counts.AddAsync([new("turn", 2, 0, 0)], Cancel);

        var week = await _counts.WeekAsync(Cancel);
        week.Day.ShouldBe([new NoticeCount("turn", 2, 0, 0)]);
        week.Week.ShouldBe([new NoticeCount("turn", 3, 0, 0)]);

        _clock.Advance(TimeSpan.FromDays(1));
        (await _counts.WeekAsync(Cancel)).Week.ShouldBe([new NoticeCount("turn", 2, 0, 0)], "the first day is eight days back now");
    }

    [Fact]
    public async Task The_first_count_of_a_day_lets_go_of_the_days_nobody_reads()
    {
        await _counts.AddAsync([new("turn", 1, 0, 0)], Cancel);
        _clock.Advance(TimeSpan.FromDays(NoticeCounts.Days));

        await _counts.AddAsync([new("turn", 1, 0, 0)], Cancel);

        _store.Forgotten.ShouldBe([new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 24)]);
        (await _counts.WeekAsync(Cancel)).Week.ShouldBe([new NoticeCount("turn", 1, 0, 0)]);
    }

    [Fact]
    public async Task Counts_kept_before_a_clean_up_that_failed_are_still_kept_and_the_failure_is_told()
    {
        _store.ForgetFails = true;
        Exception? told = null;
        var counts = new NoticeCounts(_store, _clock) { ForgetFailed = failure => told = failure };

        (await counts.AddAsync([new("turn", 1, 0, 0)], Cancel)).ShouldBeTrue();

        told.ShouldBeOfType<IOException>();
        (await counts.WeekAsync(Cancel)).Day.ShouldBe([new NoticeCount("turn", 1, 0, 0)]);
    }

    private sealed class SteppedClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>Counts in memory that refuse a write over a version another write replaced, as the real stores do.</summary>
    private sealed class VersionedCounts : INoticeCountStore
    {
        private readonly Dictionary<DateOnly, (IReadOnlyList<NoticeCount> Counts, string Version)> _held = [];
        private long _written;

        public Func<Task>? BeforeNextWrite { get; set; }

        public bool RefuseEveryWrite { get; set; }

        public int Writes { get; private set; }

        public List<DateOnly> Forgotten { get; } = [];

        public bool ForgetFails { get; set; }

        public Task<NoticeCountRead> ReadAsync(DateOnly day, CancellationToken cancellationToken = default) =>
            Task.FromResult(_held.TryGetValue(day, out var held) ? new NoticeCountRead(held.Counts, held.Version) : new NoticeCountRead([], null));

        public async Task<bool> WriteAsync(DateOnly day, IReadOnlyList<NoticeCount> counts, string? version, CancellationToken cancellationToken = default)
        {
            if (BeforeNextWrite is { } before)
            {
                BeforeNextWrite = null;
                await before();
            }

            Writes++;
            if (RefuseEveryWrite || (_held.TryGetValue(day, out var held) ? held.Version : null) != version)
            {
                return false;
            }

            _held[day] = ([.. counts], $"{++_written}");
            return true;
        }

        public Task ForgetBeforeAsync(DateOnly day, CancellationToken cancellationToken = default)
        {
            if (ForgetFails)
            {
                throw new IOException("The listing failed.");
            }

            Forgotten.Add(day);
            foreach (var old in _held.Keys.Where(held => held < day).ToArray())
            {
                _held.Remove(old);
            }

            return Task.CompletedTask;
        }
    }
}
