namespace DownfallArena.Application.Tables.Tending;

/// <summary>One page of the kept runs, newest first, how many are kept, which page of how many, and the totals.</summary>
public sealed record TendPage(IReadOnlyList<TendRun> Runs, int Total, int Page, int Pages, TendTotals Day, TendTotals Week);
