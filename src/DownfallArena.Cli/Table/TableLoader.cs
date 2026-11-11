using DownfallArena.Application.Matches.Journaling;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Picks up a recorded open table where its journal ends, when a request names it and this host does not hold it
/// (ADR 0159, ADR 0161): the registry's way to a table it was not told about. A table that never began and was
/// opened longer ago than <see cref="TableRegistry.NeverBegunFor" /> is not picked up (ADR 0115); a journal that no
/// longer fits the current engine or content is reported and preserved for the operator, and marked, so a host on
/// the same engine, content and rules does not try it again.
/// </summary>
internal sealed class TableLoader(StoredSessions stored, TableComposer composer, TimeProvider clock)
{
    /// <summary>
    /// The table, picked up again; none when it is not open, or when it cannot be picked up, which is said on the
    /// console. A store that is out is thrown (<see cref="TableRegistry.StoreFails" />): the table may be there all
    /// the same, and whoever asked tries again.
    /// </summary>
    public async Task<PlayedTable?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        TableRecord? record;
        JournalReading? journal;
        try
        {
            record = await stored.RecordAsync(id, cancellationToken);
            if (record is not { Status: TableRecord.Open })
            {
                // Asked for, and not open: if the index still lists it, its last index write failed, and nothing
                // else would ever take it out.
                if (record is not null)
                {
                    await new TableIndexer(composer.Index, id).FollowAsync(null, record, cancellationToken);
                }

                return null;
            }

            if (record.RefusedUnder is { } refused && refused == composer.Basis)
            {
                // Replayed and refused before under this very engine, content and rules: it would be refused again,
                // and only after the time a replay takes. It stays open for the operator (ADR 0115).
                return null;
            }

            journal = await composer.JournalAsync(id, cancellationToken);
        }
        catch (Exception failure) when (Refuses(failure) && !TableRegistry.StoreFails(failure))
        {
            Console.WriteLine($"  Session {id} was not picked up: {failure.Message}");
            return null;
        }

        var decided = journal?.Decisions.Count > 0;
        var now = clock.GetUtcNow();
        if (!record.Begun && !decided && now - record.CreatedAt >= TableRegistry.NeverBegunFor)
        {
            // Never began, and too long ago: the sweep closes it (ADR 0115), and nobody is asked anything meanwhile.
            return null;
        }

        PlayedTable? table;
        try
        {
            table = await composer.ResumeAsync(record, decided, cancellationToken);
        }
        catch (Exception failure) when (Refuses(failure) && !TableRegistry.StoreFails(failure))
        {
            Console.WriteLine($"  Session {id} was not picked up: {failure.Message}");
            return null;
        }

        if (table is null)
        {
            return null;
        }

        table.Touch(now);
        Console.WriteLine($"  Session {id} picked up from {journal?.Entries.Count ?? 0} recorded decisions.");
        return table;
    }

    /// <summary>
    /// Closes a table no host holds through its record, when the record and the journal both say it never began and
    /// it was opened longer ago than <see cref="TableRegistry.NeverBegunFor" /> (ADR 0115): the same reading a pick-up
    /// makes, since a record whose beginning was never written may stand over a journal of decisions.
    /// </summary>
    /// <returns>Whether the table was closed.</returns>
    public async Task<bool> ExpireAsync(string id, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (await stored.RecordAsync(id, cancellationToken) is not { Status: TableRecord.Open, Begun: false } record
            || now - record.CreatedAt < TableRegistry.NeverBegunFor
            || (await composer.JournalAsync(id, cancellationToken))?.Decisions.Count > 0)
        {
            return false;
        }

        return await stored.CloseAsync(id, composer.Index, cancellationToken);
    }

    /// <summary>
    /// Whether this key is a seat of a table that ended as a void match (ADR 0163): its record names the seat by this
    /// very key, so a stranger who guessed a table's id learns nothing.
    /// </summary>
    public async Task<bool> VoidSeatAsync(string id, string key, CancellationToken cancellationToken = default) =>
        await stored.RecordAsync(id, cancellationToken) is { Void: true } record
        && record.Seats.Any(seat => seat.Person && string.Equals(seat.Token, key, StringComparison.Ordinal));

    /// <summary>What refuses one table rather than the request: its record or journal no longer read, or no longer fit.</summary>
    internal static bool Refuses(Exception failure) =>
        failure is ArgumentException or IOException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException;
}
