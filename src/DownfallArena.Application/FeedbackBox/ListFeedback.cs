namespace DownfallArena.Application.FeedbackBox;

/// <summary>The Feedback tab's list (docs/tabletop/feedback-plan.md, R3, I1, ADR 0189).</summary>
public sealed class ListFeedback(FeedbackIndex index)
{
    public const int PerPage = 20;

    /// <summary>
    /// A page of what the filter keeps, newest first; ideas alone by how many asked, then by the newest ask, so what many
    /// players want comes first. A page past the last is the last. A filter that keeps only open feedback reads the
    /// index and opens the page's rows alone (#866); one that keeps closed feedback too reads every record.
    /// </summary>
    public async Task<FeedbackPage> HandleAsync(FeedbackFilter filter, int page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var all = filter.KeepsOpenOnly
            ? await index.RowsAsync(cancellationToken)
            : [.. (await FeedbackRecords.AllAsync(index.Records, cancellationToken)).Select(FeedbackSummary.Of)];
        var asks = FeedbackGroups.Of(all)
            .Where(group => group.Value[0].Kind == FeedbackKind.Idea)
            .ToDictionary(group => group.Key, group => FeedbackGroups.AsksOf(group.Value), StringComparer.Ordinal);
        var kept = all.Where(filter.Keeps).OrderByDescending(one => one.Id, StringComparer.Ordinal).ToList();
        if (filter.Kind == FeedbackKind.Idea)
        {
            kept = [.. kept
                .OrderByDescending(one => asks.GetValueOrDefault(one.Id)?.AskedBy ?? 1)
                .ThenByDescending(one => asks.GetValueOrDefault(one.Id)?.Latest ?? one.LastWritten)
                .ThenByDescending(one => one.Id, StringComparer.Ordinal)];
        }

        var pages = Math.Max(1, (kept.Count + PerPage - 1) / PerPage);
        var shown = Math.Clamp(page, 1, pages);
        var rows = new List<Feedback>();
        foreach (var row in kept.Skip((shown - 1) * PerPage).Take(PerPage))
        {
            if (await index.Records.FindAsync(row.Id, cancellationToken) is not { } found)
            {
                continue;
            }

            // A row the index held wrong, as a change cut short between the record and the index leaves it, is put right.
            if (FeedbackSummary.Of(found) != row)
            {
                await index.NoteAsync(found, cancellationToken);
            }

            rows.Add(found);
        }

        return new FeedbackPage(rows, kept.Count, shown, pages, rows.Where(one => asks.ContainsKey(one.Id)).ToDictionary(one => one.Id, one => asks[one.Id], StringComparer.Ordinal));
    }
}
