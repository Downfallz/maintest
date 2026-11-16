namespace DownfallArena.Application.Tables.Ports;

/// <summary>An open table as the lobby lists it.</summary>
/// <param name="Revision">The revision of the table's record the summary was taken from.</param>
/// <param name="Summary">What the lobby shows of it, as the host writes it; the index does not read it.</param>
public sealed record IndexedTable(string Table, DateTimeOffset CreatedAt, long Revision, string Summary);
