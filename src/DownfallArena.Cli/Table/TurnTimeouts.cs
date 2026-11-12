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
