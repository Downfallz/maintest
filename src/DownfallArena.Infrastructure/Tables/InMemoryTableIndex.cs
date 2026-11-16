using DownfallArena.Application.Tables.Ports;

namespace DownfallArena.Infrastructure.Tables;

/// <summary>
/// The index of the tables held in this process (ADR 0161): what a host that keeps nothing finds its tables by, and
/// what a test hands a host. It goes with the process, as the tables of such a host do.
/// </summary>
public sealed class InMemoryTableIndex : ITableIndex
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, IndexedCode> _codes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Listed> _tables = new(StringComparer.Ordinal);

    public Task<IndexedCode?> CodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        lock (_gate)
        {
            return Task.FromResult(_codes.GetValueOrDefault(code));
        }
    }

    public Task<bool> AddCodeAsync(IndexedCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        lock (_gate)
        {
            return Task.FromResult(_codes.TryAdd(code.Code, code));
        }
    }

    public Task RemoveCodeAsync(string code, string table, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        lock (_gate)
        {
            if (_codes.GetValueOrDefault(code) is { } held && held.Table == table)
            {
                _codes.Remove(code);
            }
        }

        return Task.CompletedTask;
    }

    public Task PutOpenAsync(IndexedTable table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        Put(new Listed(table, Closed: false));
        return Task.CompletedTask;
    }

    public Task CloseAsync(string table, DateTimeOffset createdAt, long revision, CancellationToken cancellationToken = default)
    {
        Put(new Listed(new IndexedTable(table, createdAt, revision, string.Empty), Closed: true));
        return Task.CompletedTask;
    }

    /// <summary>A table's entry kept, unless the one held is of the same revision of its record or a later one.</summary>
    private void Put(Listed listed)
    {
        lock (_gate)
        {
            if (_tables.GetValueOrDefault(listed.Table.Table) is not { } held || held.Table.Revision < listed.Table.Revision)
            {
                _tables[listed.Table.Table] = listed;
            }
        }
    }

    public Task<IReadOnlyList<IndexedTable>> OpenAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<IndexedTable> open = [.. _tables.Values
                .Where(listed => !listed.Closed)
                .Select(listed => listed.Table)
                .OrderByDescending(table => table.CreatedAt)
                .ThenByDescending(table => table.Table, StringComparer.Ordinal)
                .Skip(Math.Max(0, skip))
                .Take(Math.Max(0, take))];
            return Task.FromResult(open);
        }
    }

    /// <summary>An open table's entry, or what is left of it once the table closed: its revision.</summary>
    private sealed record Listed(IndexedTable Table, bool Closed);
}
