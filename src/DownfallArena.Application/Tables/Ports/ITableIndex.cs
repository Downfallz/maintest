namespace DownfallArena.Application.Tables.Ports;

/// <summary>
/// What finds a table a host does not hold (ADR 0161): the seat a join code opens, and the open tables the lobby
/// lists, newest first. It only finds: a table is its record and its journal, and whatever reads an entry here
/// checks the record before trusting it.
/// </summary>
public interface ITableIndex
{
    /// <summary>The table and seat a join code opens, or none.</summary>
    Task<IndexedCode?> CodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Gives a code to a seat, only if no seat has it. False when the code is taken.</summary>
    Task<bool> AddCodeAsync(IndexedCode code, CancellationToken cancellationToken = default);

    /// <summary>Takes a code back, only if it still opens the table named: a code given again elsewhere stays.</summary>
    Task RemoveCodeAsync(string code, string table, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes an open table's summary, unless the index already holds one of the same revision of its record or a
    /// later one: two hosts writing out of order cannot put an older summary over a newer one.
    /// </summary>
    Task PutOpenAsync(IndexedTable table, CancellationToken cancellationToken = default);

    /// <summary>Takes a table out of the open ones: it ended, or it was let go of.</summary>
    Task RemoveOpenAsync(string table, DateTimeOffset createdAt, CancellationToken cancellationToken = default);

    /// <summary>The open tables, newest first, from the first one past <paramref name="skip" />, at most <paramref name="take" />.</summary>
    Task<IReadOnlyList<IndexedTable>> OpenAsync(int skip, int take, CancellationToken cancellationToken = default);
}
