using DownfallArena.Application.Tables.Ports;

namespace DownfallArena.Application.Tables.Tending;

/// <summary>
/// The kept tending runs as the Jobs tab reads them: a page of them, newest first, and what the last day and the last
/// week of them did. A page reads its own twenty records, and the totals read only the week's, picked by their ids.
/// </summary>
public sealed class TendRuns
{
    public const int PerPage = 20;

    private readonly ITendLog _log;

    public TendRuns(ITendLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
    }

    /// <summary>A page of runs, counted from one; a page past the last is the last.</summary>
    public async Task<TendPage> PageAsync(int page, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var ids = await _log.IdsAsync(cancellationToken);
        var pages = Math.Max(1, (ids.Count + PerPage - 1) / PerPage);
        var shown = Math.Clamp(page, 1, pages);
        var runs = await ReadAsync(ids.Skip((shown - 1) * PerPage).Take(PerPage), cancellationToken);
        var week = await ReadAsync(ids.Where(id => TendRun.StartedOf(id) >= now.AddDays(-7)), cancellationToken);
        return new TendPage(runs, ids.Count, shown, pages, TendTotals.Since(week, now.AddDays(-1)), TendTotals.Since(week, now.AddDays(-7)));
    }

    public Task<TendRun?> FindAsync(string id, CancellationToken cancellationToken = default) => _log.FindAsync(id, cancellationToken);

    /// <summary>The last run kept, or none before the first: what the overview says of the job (A5).</summary>
    public async Task<TendRun?> LatestAsync(CancellationToken cancellationToken = default) =>
        (await _log.IdsAsync(cancellationToken)) is [var newest, ..] ? await _log.FindAsync(newest, cancellationToken) : null;

    // Read side by side: a week is about 170 small records, and one after another they would take seconds.
    private async Task<IReadOnlyList<TendRun>> ReadAsync(IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var read = await Task.WhenAll(ids.Select(id => _log.FindAsync(id, cancellationToken)));
        return [.. read.OfType<TendRun>()];
    }
}
