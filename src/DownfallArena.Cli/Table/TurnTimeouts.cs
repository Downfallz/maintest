using System.Globalization;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The tables whose player let their time to answer run out (ADR 0169), ended by the hourly job: a begun table with a
/// limit, whose journal was last written longer ago than its limit, is picked up, and the people it still asks lose.
/// The time is read from the store without reading the journal, so only a table past its limit is picked up.
/// </summary>
internal static class TurnTimeouts
{
    /// <summary>How long the job waits for a table it timed out to finish writing its session before it moves on.</summary>
    internal static readonly TimeSpan Closing = TimeSpan.FromMinutes(1);

    /// <summary>The time a table has waited for its next move from: its last move, or else its beginning, or else its opening.</summary>
    internal static DateTimeOffset Since(DateTimeOffset? lastMove, TableSummary summary, DateTimeOffset createdAt) =>
        lastMove ?? summary.BegunAt ?? createdAt;

    /// <summary>
    /// When a table's time to answer runs out (ADR 0169): its last move, or else its beginning, plus its limit. None for
    /// a table without a limit, one not begun, and one over.
    /// </summary>
    internal static async Task<DateTimeOffset?> AnswerByAsync(PlayedTable table, TableComposer composer)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(composer);
        if (table.TurnLimit is not { } limit || table.IsOver || !table.Session.HasBegun)
        {
            return null;
        }

        var last = await composer.LastMoveAsync(table.Id);
        return (last?.At ?? table.Session.BegunAt ?? table.CreatedAt) + limit;
    }

    /// <summary>
    /// Whether this pass is the one to warn a table's player (ADR 0169): a quarter of the limit or less is left, and
    /// it was more than that an interval of the job ago. The job runs once an interval, so one pass lands there.
    /// </summary>
    internal static bool WarnNow(TableSummary summary, DateTimeOffset since, DateTimeOffset now, TimeSpan interval)
    {
        if (summary is not { Begun: true, TurnLimitHours: { } hours })
        {
            return false;
        }

        var limit = TimeSpan.FromHours(hours);
        var left = since + limit - now;
        var quarter = limit / 4;
        return left > TimeSpan.Zero && left <= quarter && left > quarter - interval;
    }

    /// <summary>Whether a table's time to answer ran out by now: begun, with a limit, and nothing written for that long.</summary>
    internal static bool RanOut(TableSummary summary, DateTimeOffset since, DateTimeOffset now) =>
        summary is { Begun: true, TurnLimitHours: { } hours } && now - since >= TimeSpan.FromHours(hours);

    /// <summary>The tables ended because the people they asked let their time run out, by id.</summary>
    public static async Task<IReadOnlyList<string>> EndAsync(TableRegistry registry, TableComposer composer, DateTimeOffset now, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(composer);
        ArgumentNullException.ThrowIfNull(clock);
        var ended = new List<string>();
        foreach (var listed in await registry.OpenAsync())
        {
            if (listed.Summary is not { Begun: true, TurnLimitHours: not null })
            {
                continue;
            }

            var last = await composer.LastMoveAsync(listed.Id);
            var since = Since(last?.At, listed.Summary, listed.CreatedAt);
            if (!RanOut(listed.Summary, since, now) || await registry.ByIdAsync(listed.Id) is not { IsOver: false } table)
            {
                continue;
            }

            // Who is late is settled where the match ends, against the journal the time was read from: a move that
            // landed since moved its end, nobody still asked, and nothing ends.
            if ((await table.Session.TimeOutAsync(last?.Mark ?? 0)).IsSuccess)
            {
                ended.Add(listed.Id);
                await ClosedAsync(table, clock);
            }
        }

        return ended;
    }

    /// <summary>
    /// The people whose time to answer reaches its last quarter in this pass, told so on their account's devices
    /// (ADR 0169): each person asked at a table the pass finds there. The tables they sit at, by id.
    /// </summary>
    public static async Task<IReadOnlyList<string>> WarnAsync(TableRegistry registry, TableComposer composer, DateTimeOffset now, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(composer);
        var warned = new List<string>();
        if (composer.Notices is not { } notices)
        {
            return warned;
        }

        foreach (var listed in await registry.OpenAsync())
        {
            if (listed.Summary is not { Begun: true, TurnLimitHours: not null })
            {
                continue;
            }

            var last = await composer.LastMoveAsync(listed.Id);
            var since = Since(last?.At, listed.Summary, listed.CreatedAt);
            if (!WarnNow(listed.Summary, since, now, interval) || await registry.ByIdAsync(listed.Id) is not { IsOver: false } table)
            {
                continue;
            }

            var left = since + TimeSpan.FromHours(listed.Summary.TurnLimitHours.Value) - now;
            var told = false;
            foreach (var seat in table.Seats.Where(seat => seat.Person is not null && seat.Account is not null))
            {
                if (!table.Session.PlayedByItsPerson(seat.Slot) || (await table.Session.SnapshotAsync(seat.Slot)) is not { IsSuccess: true, Value.Waiting: not null })
                {
                    continue;
                }

                var payload = AccountNotices.Payload("Your turn is waiting", $"{Left(left)} left to answer, or the match is lost.", $"/play?table={Uri.EscapeDataString(table.Id)}", $"turn:{table.Id}");
                await notices.SendAsync(seat.Account!.Value, payload, "turn");
                told = true;
            }

            if (told)
            {
                warned.Add(listed.Id);
            }
        }

        return warned;
    }

    /// <summary>A time left as a notice says it, in whole hours: a quarter of a week is still counted in hours.</summary>
    internal static string Left(TimeSpan left)
    {
        var hours = Math.Max(1, (int)Math.Round(left.TotalHours));
        return hours == 1 ? "1 hour" : string.Create(CultureInfo.InvariantCulture, $"{hours} hours");
    }

    /// <summary>Waits for a table that just ended to have written its session, its result and its notices.</summary>
    private static async Task ClosedAsync(PlayedTable table, TimeProvider clock)
    {
        var deadline = clock.GetUtcNow() + Closing;
        while (!table.IsFinished && clock.GetUtcNow() < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), clock);
        }
    }
}
