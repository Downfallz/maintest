using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The index of feedback (#866): what the operator's list, the Overview's count and the spam count read in place of every
/// record. <see cref="Records" /> is the store every use case writes through, which writes the index with each save and
/// delete; a write names the version it read, and reads again when another process wrote first. Each read checks the
/// index against a listing of the store, which reads no record: what another process saved without it, or let go of,
/// is opened or dropped then, so the index never lacks a piece of feedback for long. Nothing here waits on another
/// write: the players' sending, under <see cref="FeedbackWrites" />, never waits on an operator's read.
/// </summary>
public sealed class FeedbackIndex
{
    /// <summary>How many times a change is read and written again when another write keeps coming first.</summary>
    public const int Tries = 8;

    private readonly IFeedbackStore _records;
    private readonly IFeedbackIndexStore _kept;
    private readonly TimeProvider _clock;

    public FeedbackIndex(IFeedbackStore records, IFeedbackIndexStore kept, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(kept);
        ArgumentNullException.ThrowIfNull(clock);
        (_records, _kept, _clock) = (records, kept, clock);
        Records = new Indexed(records, this);
    }

    /// <summary>The store, writing the index with each save and delete: what the use cases are given.</summary>
    public IFeedbackStore Records { get; }

    /// <summary>
    /// The rows the index holds now: the open feedback, and the spam marks within their window. The index is first
    /// brought in line with the store's listing; the first read on a store with no index builds it, opening every record once.
    /// </summary>
    public async Task<IReadOnlyList<FeedbackSummary>> RowsAsync(CancellationToken cancellationToken = default)
    {
        // The index before the listing: a save made between the two moves the version, so this write is refused.
        var read = await _kept.ReadIndexAsync(cancellationToken);
        var listed = (await _records.IdsAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var now = _clock.GetUtcNow();
        var content = read.Content ?? FeedbackIndexContent.Empty;
        var known = content.Rows.Select(row => row.Id).Concat(content.Closed).ToHashSet(StringComparer.Ordinal);
        var missing = listed.Where(id => !known.Contains(id)).Order(StringComparer.Ordinal).ToList();
        if (read.Content is not null && missing.Count == 0 && known.All(listed.Contains) && content.Rows.All(row => row.HeldAt(now)))
        {
            return content.Rows;
        }

        var rows = content.Rows.Where(row => listed.Contains(row.Id)).ToList();
        var closed = content.Closed.Where(listed.Contains).ToList();
        foreach (var id in missing)
        {
            // One that cannot be read is kept by its id, so it is not opened at every read; the list skipped it before too.
            if (await _records.FindAsync(id, cancellationToken) is { } found)
            {
                rows.Add(FeedbackSummary.Of(found));
            }
            else
            {
                closed.Add(id);
            }
        }

        var built = Tidied(rows, closed, now);
        await _kept.WriteIndexAsync(built, read.Version, cancellationToken);
        return built.Rows;
    }

    /// <summary>Puts the index's row right for feedback a read found it held wrong, as it is kept now.</summary>
    public Task NoteAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        var summary = FeedbackSummary.Of(feedback);
        return ChangeAsync(content => Placed(content, summary.Id, summary), cancellationToken);
    }

    private Task ForgetAsync(string id, CancellationToken cancellationToken) => ChangeAsync(content => Placed(content, id, null), cancellationToken);

    /// <summary>
    /// Reads the index, changes it and writes it over the version read, reading again while another write comes first.
    /// None written yet, the change starts it: the next read finds the rest by the listing.
    /// </summary>
    private async Task ChangeAsync(Func<FeedbackIndexContent, FeedbackIndexContent> change, CancellationToken cancellationToken)
    {
        for (var tried = 0; tried < Tries; tried++)
        {
            var read = await _kept.ReadIndexAsync(cancellationToken);
            if (await _kept.WriteIndexAsync(change(read.Content ?? FeedbackIndexContent.Empty), read.Version, cancellationToken))
            {
                return;
            }
        }

        // Every try lost to another write: the list puts the row right when it opens the record (ListFeedback).
    }

    private FeedbackIndexContent Placed(FeedbackIndexContent content, string id, FeedbackSummary? summary)
    {
        var rows = content.Rows.Where(row => row.Id != id).ToList();
        var closed = content.Closed.Where(one => one != id).ToList();
        if (summary is not null)
        {
            rows.Add(summary);
        }

        return Tidied(rows, closed, _clock.GetUtcNow());
    }

    /// <summary>The rows the index holds as rows at that time, the rest by their ids, each in the order ids sort.</summary>
    private static FeedbackIndexContent Tidied(List<FeedbackSummary> rows, List<string> closed, DateTimeOffset now) => new(
        [.. rows.Where(row => row.HeldAt(now)).OrderBy(row => row.Id, StringComparer.Ordinal)],
        [.. closed.Concat(rows.Where(row => !row.HeldAt(now)).Select(row => row.Id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);

    /// <summary>The store as the use cases are given it: each save and delete writes the index after the record.</summary>
    private sealed class Indexed(IFeedbackStore inner, FeedbackIndex index) : IFeedbackStore
    {
        public async Task SaveAsync(Feedback feedback, CancellationToken cancellationToken = default)
        {
            await inner.SaveAsync(feedback, cancellationToken);
            await index.NoteAsync(feedback, cancellationToken);
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            await inner.DeleteAsync(id, cancellationToken);
            await index.ForgetAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default) => inner.IdsAsync(cancellationToken);

        public Task<IReadOnlyList<string>> IdsSentAsync(DateTimeOffset after, DateTimeOffset until, CancellationToken cancellationToken = default) =>
            inner.IdsSentAsync(after, until, cancellationToken);

        public Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default) => inner.FindAsync(id, cancellationToken);
    }
}
