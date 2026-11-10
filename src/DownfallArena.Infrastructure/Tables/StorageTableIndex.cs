using System.Globalization;
using Azure;
using Azure.Data.Tables;
using DownfallArena.Application.Tables.Ports;

namespace DownfallArena.Infrastructure.Tables;

/// <summary>
/// The index of the tables in Azure Table Storage (ADR 0161), one table of the account that holds the runs: a
/// partition of join codes keyed by the code, and a partition of open tables keyed so the newest sorts first.
/// Every write is on the entity's tag or refused as a conflict, so two hosts cannot both take one code or put
/// an older summary over a newer one.
/// </summary>
public sealed class StorageTableIndex : ITableIndex
{
    /// <summary>The name of the table the index is kept in.</summary>
    public const string Name = "tableindex";

    private const string Codes = "code";
    private const string Open = "open";

    private readonly TableClient _table;

    private StorageTableIndex(TableClient table) => _table = table;

    /// <summary>The index in this table, made if it is not there yet, so an index handed out can be written to at once.</summary>
    public static async Task<StorageTableIndex> OpenAsync(TableClient table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        await table.CreateIfNotExistsAsync(cancellationToken);
        return new StorageTableIndex(table);
    }

    public async Task<IndexedCode?> CodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var held = await _table.GetEntityIfExistsAsync<TableEntity>(Codes, code, cancellationToken: cancellationToken);
        return held.HasValue && held.Value is { } entity
            ? new IndexedCode(code, entity.GetString(nameof(IndexedCode.Table)), entity.GetString(nameof(IndexedCode.Seat)))
            : null;
    }

    public async Task<bool> AddCodeAsync(IndexedCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        try
        {
            await _table.AddEntityAsync(
                new TableEntity(Codes, code.Code) { [nameof(IndexedCode.Table)] = code.Table, [nameof(IndexedCode.Seat)] = code.Seat },
                cancellationToken);
            return true;
        }
        catch (RequestFailedException failure) when (failure.Status == 409)
        {
            return false;
        }
    }

    public async Task RemoveCodeAsync(string code, string table, CancellationToken cancellationToken = default)
    {
        var held = await _table.GetEntityIfExistsAsync<TableEntity>(Codes, code, cancellationToken: cancellationToken);
        if (!held.HasValue || held.Value is not { } entity || entity.GetString(nameof(IndexedCode.Table)) != table)
        {
            return;
        }

        try
        {
            await _table.DeleteEntityAsync(Codes, code, entity.ETag, cancellationToken);
        }
        catch (RequestFailedException failure) when (failure.Status is 404 or 412)
        {
            // Taken back or given again meanwhile: either way it is no longer this table's to take back.
        }
    }

    public async Task PutOpenAsync(IndexedTable table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        var row = RowOf(table.Table, table.CreatedAt);
        while (true)
        {
            var held = await _table.GetEntityIfExistsAsync<TableEntity>(Open, row, cancellationToken: cancellationToken);
            var entity = new TableEntity(Open, row)
            {
                [nameof(IndexedTable.Table)] = table.Table,
                [nameof(IndexedTable.CreatedAt)] = table.CreatedAt,
                [nameof(IndexedTable.Revision)] = table.Revision,
                [nameof(IndexedTable.Summary)] = table.Summary,
            };
            try
            {
                if (!held.HasValue || held.Value is not { } current)
                {
                    await _table.AddEntityAsync(entity, cancellationToken);
                }
                else if ((current.GetInt64(nameof(IndexedTable.Revision)) ?? 0) < table.Revision)
                {
                    await _table.UpdateEntityAsync(entity, current.ETag, TableUpdateMode.Replace, cancellationToken);
                }

                return;
            }
            catch (RequestFailedException failure) when (failure.Status is 409 or 412)
            {
                // Another host wrote it between the read and the write: read it again.
            }
        }
    }

    public async Task RemoveOpenAsync(string table, DateTimeOffset createdAt, CancellationToken cancellationToken = default)
    {
        try
        {
            await _table.DeleteEntityAsync(Open, RowOf(table, createdAt), ETag.All, cancellationToken);
        }
        catch (RequestFailedException failure) when (failure.Status == 404)
        {
            // Already gone.
        }
    }

    public async Task<IReadOnlyList<IndexedTable>> OpenAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var open = new List<IndexedTable>();
        var passed = 0;
        await foreach (var entity in _table.QueryAsync<TableEntity>(entity => entity.PartitionKey == Open, cancellationToken: cancellationToken))
        {
            if (open.Count >= take)
            {
                break;
            }

            if (passed++ < skip)
            {
                continue;
            }

            open.Add(new IndexedTable(
                entity.GetString(nameof(IndexedTable.Table)),
                entity.GetDateTimeOffset(nameof(IndexedTable.CreatedAt)) ?? DateTimeOffset.MinValue,
                entity.GetInt64(nameof(IndexedTable.Revision)) ?? 0,
                entity.GetString(nameof(IndexedTable.Summary))));
        }

        return open;
    }

    /// <summary>
    /// The row of an open table: the time until the end of the calendar, so a query in key order is newest first,
    /// then the table's id, so two tables opened in the same tick are two rows.
    /// </summary>
    private static string RowOf(string table, DateTimeOffset createdAt) =>
        string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.MaxValue.UtcTicks - createdAt.UtcTicks:D19}-{table}");
}
