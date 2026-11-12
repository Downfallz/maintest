namespace DownfallArena.Cli.Table;

/// <summary>
/// The begun games whose seat was offered to another player and that nobody took (ADR 0170), ended by the hourly
/// job: a seat still open and no move for a day, and the team nobody holds gives the match up, as its player
/// would have by conceding. Both seats open, and the game ends as a timeout does: whoever it still asks loses.
/// </summary>
internal static class AbandonedSeats
{
    /// <summary>How long a begun game waits, with a seat open, for anyone to move before it ends.</summary>
    internal static readonly TimeSpan For = TimeSpan.FromHours(24);

    /// <summary>Whether a begun game with a seat open has had no move for <see cref="For" /> by now.</summary>
    internal static bool Due(TableSummary summary, DateTimeOffset since, DateTimeOffset now) =>
        summary is { Begun: true, OpenSeatCount: > 0 } && now - since >= For;

    /// <summary>The games ended because nobody took the seat their player left, by id.</summary>
    public static async Task<IReadOnlyList<string>> EndAsync(TableRegistry registry, TableComposer composer, DateTimeOffset now, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(composer);
        ArgumentNullException.ThrowIfNull(clock);
        var ended = new List<string>();
        foreach (var listed in await registry.OpenAsync())
        {
            if (listed.Summary is not { Begun: true, OpenSeatCount: > 0 })
            {
                continue;
            }

            var last = await composer.LastMoveAsync(listed.Id);
            if (!Due(listed.Summary, TurnTimeouts.Since(last?.At, listed.Summary, listed.CreatedAt), now)
                || await registry.ByIdAsync(listed.Id) is not { IsOver: false } table)
            {
                continue;
            }

            // Read again off the table as it was picked up: a seat somebody took since the index was read is not open.
            var open = table.Seats.Where(seat => seat.Person is { HasArrived: false } && table.OpenSeats.Contains(seat.Name)).ToList();
            var given = open.Count switch
            {
                0 => null,
                1 => await table.Session.ConcedeAsync(open[0].Slot),
                _ => await table.Session.TimeOutAsync(last?.Mark ?? 0),
            };
            if (given is { IsSuccess: true })
            {
                ended.Add(listed.Id);
                await TurnTimeouts.ClosedAsync(table, clock);
            }
        }

        return ended;
    }
}
