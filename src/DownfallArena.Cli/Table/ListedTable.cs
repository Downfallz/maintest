namespace DownfallArena.Cli.Table;

/// <summary>
/// An open table as a listing reads it (ADR 0161): its summary, from the index, or from the table itself when this
/// host holds it, which is the fresher of the two.
/// </summary>
/// <param name="Held">The table, when this host holds it; a listing that needs more than the summary loads it.</param>
internal sealed record ListedTable(string Id, DateTimeOffset CreatedAt, TableSummary Summary, PlayedTable? Held);
