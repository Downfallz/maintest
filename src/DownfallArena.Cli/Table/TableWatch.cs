namespace DownfallArena.Cli.Table;

/// <summary>
/// What a host does so that it sees what other processes write to the tables it holds (ADR 0162): about once a
/// second, for every unfinished table somebody asked for lately, where its journal ends and its record's revision.
/// A journal that moved -- a turn the worker took -- is read again, and its pages told (question 9). A record another
/// process wrote -- an invitation the hourly job expired, a table it closed -- makes the host let the table go, and
/// the next request picks it up again as the record says; except a record that says the match finished, which no
/// request could pick up again: that table is read to its end instead, so its players see the outcome. A finished
/// table is looked at too, for its record alone: one the operator voided elsewhere is let go of, so its seats are
/// answered with the explanation rather than served from what this host holds (ADR 0176).
/// </summary>
/// <remarks>
/// Two reads a table a second, the journal's length and the record, for the tables asked for in the last ten
/// minutes; one, the record, for a finished table. A table nobody asked for since is read again when somebody does, which is when it matters.
/// </remarks>
internal sealed class TableWatch(TableRegistry registry, TimeProvider clock)
{
    /// <summary>How often the tables held are looked at.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(1);

    /// <summary>How long after its last request a table is still looked at.</summary>
    public static readonly TimeSpan WatchedFor = TimeSpan.FromMinutes(10);

    /// <summary>One look at every table watched. How many were moved or let go of.</summary>
    public async Task<int> WatchOnceAsync(CancellationToken cancellationToken = default)
    {
        var looks = await LooksAsync(cancellationToken);
        return looks.Count(look => look == true);
    }

    // One look at each table asked for lately, finished or not, in turn: whether each moved, or null where it could not be made.
    private async Task<List<bool?>> LooksAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var looks = new List<bool?>();
        foreach (var table in registry.HeldTables().Where(table => now - table.Touched < WatchedFor))
        {
            looks.Add(await LookAsync(table, cancellationToken));
        }

        return looks;
    }

    /// <summary>
    /// One look at every table held, as a relay's connection opens (ADR 0165), that fails when any look could not be
    /// made: nothing comes to look again on a clock, so the store being out has to be said, and the look made again.
    /// </summary>
    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var failed = (await LooksAsync(cancellationToken)).Count(look => look is null);
        if (failed > 0)
        {
            throw new IOException($"{failed} table{(failed == 1 ? string.Empty : "s")} could not be looked at.");
        }
    }

    /// <summary>
    /// One look at one table, the one another process said it moved (ADR 0165): whether this host holds it, and then as <see cref="WatchOnceAsync" /> looks at each. A look that could not be made fails, so that what
    /// heard the move can have it looked at again.
    /// </summary>
    public async Task MovedAsync(string table, CancellationToken cancellationToken = default)
    {
        if (registry.Held(table) is not { } held)
        {
            return;
        }

        // A reaction another process kept is one of the things that moved (ADR 0173): read again at the next view.
        held.Api.Reactions.Stale();
        if (await LookAsync(held, cancellationToken) is null)
        {
            throw new IOException($"Session {table} could not be looked at.");
        }
    }

    // Whether the table moved or was let go of; null when the look could not be made.
    private async Task<bool?> LookAsync(PlayedTable table, CancellationToken cancellationToken)
    {
        try
        {
            if (table.Run is { } run && await run.RewrittenAsync(cancellationToken) is { } rewritten)
            {
                if (rewritten.Void)
                {
                    // Voided elsewhere, finished or not (ADR 0176): its seats are answered from the record from now on.
                    Console.WriteLine($"  Session {table.Id} was voided by another process: let go of.");
                    registry.LetGo(table.Id);
                }
                else if (table.IsOver)
                {
                    // A finished table here moves no further, whatever else another process wrote.
                    return false;
                }
                else if (rewritten.Status == TableRecord.Finished)
                {
                    // Finished elsewhere -- the worker took the last turn: the journal holds the end, and the
                    // players read it here, where their seats are.
                    await table.Session.CatchUpAsync(cancellationToken);
                }
                else
                {
                    Console.WriteLine($"  Session {table.Id} was written by another process: let go of, and picked up again at the next request.");
                    registry.LetGo(table.Id);
                }

                return true;
            }

            return !table.IsOver && await table.Session.CatchUpAsync(cancellationToken);
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is ObjectDisposedException)
        {
            // The store is out for a moment, or the table was let go of under the look: the next look tries.
            return failure is ObjectDisposedException ? false : null;
        }
    }

    /// <summary>Looks every <see cref="Every" /> until the host stops.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await WatchOnceAsync(cancellationToken);
                await Task.Delay(Every, clock, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
