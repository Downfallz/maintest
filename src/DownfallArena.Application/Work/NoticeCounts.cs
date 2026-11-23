using DownfallArena.Application.Ports;

namespace DownfallArena.Application.Work;

/// <summary>
/// The notices worker's counts (notifications plan, N6): what it sent, what push services refused and what was for a
/// device that is gone, a kind each, added to the day's document after each look at the queue, and read by the
/// operator's Jobs tab as today and the last seven days. The API and the worker app both send, so a count is added
/// over the version read, and read again on a conflict: no lock of one process protects it.
/// </summary>
public sealed class NoticeCounts
{
    /// <summary>How many days the tab reads, today included; older days are let go of.</summary>
    public const int Days = 7;

    /// <summary>How many times an addition is read again after another process wrote first, before it is given up.</summary>
    public const int MostTries = 8;

    private readonly INoticeCountStore _store;
    private readonly TimeProvider _clock;

    public NoticeCounts(INoticeCountStore store, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Adds the counts to today's. False when other processes kept writing first, <see cref="MostTries" /> times: the
    /// counts are lost, never the notices. The first addition of a day lets go of the days nobody reads any more.
    /// </summary>
    public async Task<bool> AddAsync(IReadOnlyCollection<NoticeCount> counted, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(counted);
        if (counted.All(count => count.IsEmpty))
        {
            return true;
        }

        var day = Today();
        for (var tried = 0; tried < MostTries; tried++)
        {
            var read = await _store.ReadAsync(day, cancellationToken);
            if (await _store.WriteAsync(day, NoticeCount.Sum([.. read.Counts, .. counted]), read.Version, cancellationToken))
            {
                if (read.Version is null)
                {
                    await _store.ForgetBeforeAsync(day.AddDays(1 - Days), cancellationToken);
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>Today's counts and the last <see cref="Days" /> days', today included.</summary>
    public async Task<NoticeWeek> WeekAsync(CancellationToken cancellationToken = default)
    {
        var today = Today();
        var days = new List<IReadOnlyList<NoticeCount>>(Days);
        for (var back = 0; back < Days; back++)
        {
            days.Add((await _store.ReadAsync(today.AddDays(-back), cancellationToken)).Counts);
        }

        return new NoticeWeek(today, NoticeCount.Sum(days[0]), NoticeCount.Sum(days.SelectMany(counts => counts)));
    }

    private DateOnly Today() => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
}
