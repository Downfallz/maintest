namespace DownfallArena.Cli.Table;

/// <summary>
/// Rebuilds, when a host starts, the tables the host before it left unfinished (ADR 0091): every run in the
/// store whose record says it is open and whose last decision is recent enough that somebody may come back
/// to it. A match lives in memory and went with the replica; its seed and its decisions did not.
/// </summary>
/// <remarks>
/// What is left alone, and why: a record that says finished or closed (nothing to come back to), a run with no
/// record (a batch, or a host before this decision), and a table nobody has touched for longer than the
/// registry would have kept it (<see cref="TableRegistry.IdleFor" />) -- the same fate the sweep gives a table
/// nobody is at, read off the record instead of a timer. A replay that does not reach the end of its record
/// is reported and left where it was.
/// </remarks>
internal static class TableRestorer
{
    /// <summary>Rebuilds what can be rebuilt, into the registry, and says what happened to each, one line a table.</summary>
    public static async Task<IReadOnlyList<string>> RestoreAsync(StoredSessions stored, TableComposer composer, TableRegistry registry, TimeProvider clock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(composer);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(clock);

        var said = new List<string>();
        var now = clock.GetUtcNow();
        foreach (var session in await stored.ListAsync(cancellationToken))
        {
            if (await stored.RecordAsync(session.Id, cancellationToken) is not { Status: TableRecord.Open } record)
            {
                continue;
            }

            var journal = await stored.JournalAsync(session.Id, cancellationToken);
            var touched = journal.Count > 0 ? journal[^1].At : record.CreatedAt;
            if (now - touched >= TableRegistry.IdleFor)
            {
                // Left as it is, not marked: the record still says what the table was, and the sweep rule is
                // what says nobody is coming back.
                said.Add($"Session {record.Id} left as it was: nobody has been at it since {touched:u}.");
                continue;
            }

            PlayedTable? table;
            try
            {
                table = await composer.ResumeAsync(record, journal, cancellationToken);
            }
            catch (Exception failure) when (failure is ArgumentException or IOException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException)
            {
                said.Add($"Session {record.Id} was not rebuilt: {failure.Message}");
                continue;
            }

            if (table is null)
            {
                continue;
            }

            if (!registry.TryRestore(table, record.Seats.ToDictionary(seat => seat.Slot, seat => seat.Code, StringComparer.Ordinal)))
            {
                table.Dispose();
                said.Add($"Session {record.Id} was not rebuilt: this host has as many tables under way as it takes.");
                continue;
            }

            table.Touch(now);
            said.Add($"Session {record.Id} rebuilt from {journal.Count} recorded decisions; its seats answer to the links and codes they had.");
        }

        return said;
    }
}
