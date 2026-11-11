namespace DownfallArena.Cli.Table;

/// <summary>
/// What a host does with the recorded tables as it starts (ADR 0159, ADR 0161): nothing is replayed -- a table is
/// picked up at the first request that names it (<see cref="TableLoader" />) -- but the index is put back in line
/// with the records, which is what lists and finds them, and the keys minted before a key named its table are told
/// to the registry, for the life of their table.
/// </summary>
/// <remarks>
/// The index is put right from the records here because it can be behind them: a record written before the index
/// existed, or an index write that failed, which for a table that ended is its last and is never put right by a
/// next one. Every open record is put back, and every table the index lists whose record is not open any more is
/// taken out of it.
/// </remarks>
internal static class TableRestorer
{
    /// <summary>Puts the index back in line with the records and the legacy keys into the registry, and says how many, in one line.</summary>
    public static async Task<IReadOnlyList<string>> RestoreAsync(StoredSessions stored, TableRegistry registry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(registry);

        var said = new List<string>();
        var open = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in (await stored.ListAsync(cancellationToken)).Select(session => session.Id))
        {
            if (await ReadAsync(stored, id, said, cancellationToken) is not { Record: { Status: TableRecord.Open } record })
            {
                continue;
            }

            await new TableIndexer(registry.Index, id).FollowAsync(null, record, cancellationToken);
            foreach (var key in record.Seats.SelectMany(seat => new[] { seat.Token, seat.Invitation }).Append(record.PilotToken).OfType<string>())
            {
                registry.Remember(key, id);
            }

            open.Add(id);
        }

        var closed = 0;
        foreach (var listed in await registry.Index.OpenAsync(0, TableRegistry.Listing, cancellationToken))
        {
            if (open.Contains(listed.Table))
            {
                continue;
            }

            // Listed, and not open in its record: it ended, or its run is gone. Out of the index at the record's
            // revision, or for good when there is no record left to say anything. A record that cannot be read
            // says neither, and the entry is left as it is: a store out for a moment must not end a table for good.
            var read = await ReadAsync(stored, listed.Table, said, cancellationToken);
            if (read is not { } reading || reading.Record is { Status: TableRecord.Open })
            {
                continue;
            }

            await registry.Index.CloseAsync(listed.Table, listed.CreatedAt, reading.Record?.Revision ?? long.MaxValue, cancellationToken);
            closed++;
        }

        said.Add($"{open.Count} open table{(open.Count == 1 ? string.Empty : "s")} indexed{(closed > 0 ? $", {closed} no longer open taken out of the index" : string.Empty)}; each is picked up at the first request that names it.");
        return said;
    }

    /// <summary>A run's record as read, none in it when the run has none; no reading at all when it could not be read.</summary>
    private static async Task<Reading?> ReadAsync(StoredSessions stored, string id, List<string> said, CancellationToken cancellationToken)
    {
        try
        {
            return new Reading(await stored.RecordAsync(id, cancellationToken));
        }
        catch (Exception failure) when (TableLoader.Refuses(failure) || TableRegistry.StoreFails(failure))
        {
            said.Add($"Session {id} was not indexed: {failure.Message}");
            return null;
        }
    }

    private sealed record Reading(TableRecord? Record);
}
