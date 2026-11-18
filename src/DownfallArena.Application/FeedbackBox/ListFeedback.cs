using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>The Feedback tab's list (docs/tabletop/feedback-plan.md, R3, I1, ADR 0189).</summary>
public sealed class ListFeedback(IFeedbackStore store)
{
    public const int PerPage = 20;

    /// <summary>
    /// A page of what the filter keeps, newest first; ideas alone by how many asked, then by the newest ask, so what many
    /// players want comes first. A page past the last is the last.
    /// </summary>
    public async Task<FeedbackPage> HandleAsync(FeedbackFilter filter, int page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var all = await FeedbackRecords.AllAsync(store, cancellationToken);
        var asks = FeedbackGroups.Of(all)
            .Where(group => group.Value[0].Kind == FeedbackKind.Idea)
            .ToDictionary(group => group.Key, group => FeedbackGroups.AsksOf(group.Value), StringComparer.Ordinal);
        var kept = all.Where(filter.Keeps).ToList();
        if (filter.Kind == FeedbackKind.Idea)
        {
            kept = [.. kept
                .OrderByDescending(one => asks.GetValueOrDefault(one.Id)?.AskedBy ?? 1)
                .ThenByDescending(one => asks.GetValueOrDefault(one.Id)?.Latest ?? one.LastWritten)
                .ThenByDescending(one => one.Id, StringComparer.Ordinal)];
        }

        var pages = Math.Max(1, (kept.Count + PerPage - 1) / PerPage);
        var shown = Math.Clamp(page, 1, pages);
        var rows = kept.Skip((shown - 1) * PerPage).Take(PerPage).ToList();
        return new FeedbackPage(rows, kept.Count, shown, pages, rows.Where(one => asks.ContainsKey(one.Id)).ToDictionary(one => one.Id, one => asks[one.Id], StringComparer.Ordinal));
    }
}
