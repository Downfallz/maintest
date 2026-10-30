namespace DownfallArena.Cli.Table;

/// <summary>
/// Rebuilds every recorded open table when a host starts. Open games do not expire during a pause.
/// Finished and explicitly closed records are left alone, as are runs with no table record. A replay
/// that no longer fits the current engine or content is reported and preserved for the operator.
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

            var journal = await stored.JournalAsync(id, cancellationToken);
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
