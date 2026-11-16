using DownfallArena.Application.Tables.Ports;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Keeps the index of the tables (ADR 0161) in line with one table's record, each time the record is written: its
/// seats' codes given to the index and those it no longer has taken back, and its summary among the open tables
/// while it is open. A write that fails is a line on the console and nothing else: the record is the table, and
/// its next write puts the index right.
/// </summary>
/// <remarks>Its writes take turns: the record's writes that call it do.</remarks>
internal sealed class TableIndexer(ITableIndex index, string table)
{
    // Codes a seat no longer has that the index has not taken back yet: an index write that failed leaves them
    // here, for the next record write to take back.
    private readonly HashSet<string> _owedCodes = new(StringComparer.Ordinal);

    public ITableIndex Index => index;

    /// <param name="before">The record as it was before this write, which says which codes the table had.</param>
    public async Task FollowAsync(TableRecord? before, TableRecord written, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(written);
        try
        {
            // A table a host refused to pick up stays listed, its summary saying under what: a host on that basis
            // leaves it to the operator (ADR 0115), and one on another basis tries it again.
            var listed = written.Status == TableRecord.Open;
            var codes = listed
                ? written.Seats.Where(seat => seat.Code is not null).ToDictionary(seat => seat.Code!, seat => seat.Slot, StringComparer.Ordinal)
                : [];
            // Owed before anything is written: a code this record no longer has is kept until the index has taken
            // it back, since the next record will not have it either and would never say it again.
            _owedCodes.UnionWith((before?.Seats ?? []).Select(seat => seat.Code).OfType<string>().Where(code => !codes.ContainsKey(code)));
            foreach (var (code, seat) in codes)
            {
                if (!await index.AddCodeAsync(new IndexedCode(code, written.Id, seat), cancellationToken)
                    && await index.CodeAsync(code, cancellationToken) is { } held && (held.Table != written.Id || held.Seat != seat))
                {
                    Console.WriteLine($"  Session {table}: the code of {seat} is another table's in the index, which keeps it.");
                }
            }

            foreach (var code in _owedCodes.Where(code => !codes.ContainsKey(code)).ToList())
            {
                await index.RemoveCodeAsync(code, written.Id, cancellationToken);
                _owedCodes.Remove(code);
            }

            if (listed)
            {
                await index.PutOpenAsync(new IndexedTable(written.Id, written.CreatedAt, written.Revision, TableSummary.Of(written).ToJson()), cancellationToken);
            }
            else
            {
                await index.CloseAsync(written.Id, written.CreatedAt, written.Revision, cancellationToken);
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or Azure.RequestFailedException or ArgumentException)
        {
            Console.WriteLine($"  Session {table} could not be written into the index of the tables: {failure.Message}");
        }
    }
}
