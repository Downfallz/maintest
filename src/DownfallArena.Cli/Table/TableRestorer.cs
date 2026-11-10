namespace DownfallArena.Cli.Table;

/// <summary>
/// What a host does with the recorded open tables as it starts (ADR 0159, ADR 0161): nothing is replayed -- a
/// table is picked up at the first request that names it (<see cref="TableLoader" />) -- but each one is put back
/// into the index, as its record says, which is what lists and finds it, and the keys minted before a key named
/// its table are told to the registry, for the life of their table.
/// </summary>
/// <remarks>
/// The index is put right from the records here because it is one of them behind: a record written before the
/// index existed, or an index write that failed. Finished and explicitly closed records are left alone, as are
/// runs with no table record.
/// </remarks>
internal static class TableRestorer
{
    /// <summary>Puts every open table back into the index and its legacy keys into the registry, and says how many, in one line.</summary>
    public static async Task<IReadOnlyList<string>> RestoreAsync(StoredSessions stored, TableRegistry registry, ReplayBasis? basis, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(registry);

        var said = new List<string>();
        var open = 0;
        foreach (var id in (await stored.ListAsync(cancellationToken)).Select(session => session.Id))
        {
            TableRecord? record;
            try
            {
                record = await stored.RecordAsync(id, cancellationToken);
            }
            catch (Exception failure) when (TableLoader.Refuses(failure))
            {
                said.Add($"Session {id} was not indexed: {failure.Message}");
                continue;
            }

            if (record is not { Status: TableRecord.Open })
            {
                continue;
            }

            // Refused under another engine, content or rules than this host's: this host tries it again, so the
            // lobby lists it again. Refused under this very basis, it stays the operator's (ADR 0115).
            var listed = record.RefusedUnder is { } refused && refused != basis ? record with { RefusedUnder = null } : record;
            await new TableIndexer(registry.Index, id).FollowAsync(null, listed, cancellationToken);
            foreach (var key in record.Seats.SelectMany(seat => new[] { seat.Token, seat.Invitation }).Append(record.PilotToken).OfType<string>())
            {
                registry.Remember(key, id);
            }

            open++;
        }

        said.Add($"{open} open table{(open == 1 ? string.Empty : "s")} indexed; each is picked up at the first request that names it.");
        return said;
    }
}
