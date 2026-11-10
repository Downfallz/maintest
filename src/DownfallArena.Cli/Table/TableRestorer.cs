namespace DownfallArena.Cli.Table;

/// <summary>
/// Picks up every recorded open table when a host starts, each where its journal ends (ADR 0159). A game that began does not expire during a pause.
/// Finished and explicitly closed records are left alone, as are runs with no table record, and a table that
/// never began and was opened longer ago than <see cref="TableRegistry.NeverBegunFor" /> (ADR 0115). A journal
/// that no longer fits the current engine or content is reported and preserved for the operator, and marked, so a
/// host on the same engine, content and rules does not rebuild it again.
/// </summary>
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
        foreach (var id in (await stored.ListAsync(cancellationToken)).Select(session => session.Id))
        {
            // A table this host already holds is its own, and open: rebuilt beside itself it would be the same
            // match twice, under the same id and the same tokens.
            if (registry.ById(id) is not null || await stored.RecordAsync(id, cancellationToken) is not { Status: TableRecord.Open } record)
            {
                continue;
            }

            if (record.RefusedUnder is { } refused && refused == composer.Basis)
            {
                // Replayed and refused before under this very engine, content and rules: it would be refused again,
                // and only after the time a replay takes. It stays open for the operator (ADR 0115).
                said.Add($"Session {record.Id} left as it was: its replay was refused before, under this engine, content and rules.");
                continue;
            }

            // Read inside what refuses one table: a line no host can read is that table's to be left over, and the
            // tables after it are picked up all the same.
            Application.Matches.Journaling.JournalReading? journal;
            try
            {
                journal = await composer.JournalAsync(id, cancellationToken);
            }
            catch (Exception failure) when (Refuses(failure))
            {
                said.Add($"Session {record.Id} was not rebuilt: {failure.Message}");
                continue;
            }

            var lines = journal?.Entries.Count ?? 0;
            var decided = journal?.Decisions.Count > 0;
            if (!record.Begun && !decided && now - record.CreatedAt >= TableRegistry.NeverBegunFor)
            {
                // A table that never began is let go of by the sweep after the same time (ADR 0115); one the
                // host before this one never got to sweep is left as it is, not marked, and not rebuilt.
                said.Add($"Session {record.Id} left as it was: it never began, and was opened at {record.CreatedAt:u}.");
                continue;
            }

            PlayedTable? table;
            try
            {
                table = await composer.ResumeAsync(record, decided, cancellationToken);
            }
            catch (Exception failure) when (Refuses(failure))
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
            said.Add($"Session {record.Id} rebuilt from {lines} recorded decisions; its seats answer to the links and codes they had.");
        }

        return said;
    }

    /// <summary>What refuses one table rather than the host's start: its record or journal no longer read, or no longer fit.</summary>
    private static bool Refuses(Exception failure) =>
        failure is ArgumentException or IOException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException;
}
