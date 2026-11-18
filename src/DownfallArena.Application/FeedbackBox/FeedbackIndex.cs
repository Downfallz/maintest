using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The index of feedback (#866): what the operator's list, the Overview's count and the spam count read in place of every
/// record. <see cref="Records" /> is the store every use case writes through. A save first takes the record's id out of
/// the index, then writes the record, then places the record as it is kept then; a write of the index names the version
/// it read, and reads again when another process wrote first. Each read checks the index against a listing of the
/// store, which reads no record: an id the index lacks is opened, and one no longer listed dropped. So a save cut short
/// after its record, by a hiccup or a crash, leaves its id missing, never its row wrong, and the next read puts it back.
/// Nothing here waits on another write: the players' sending, under <see cref="FeedbackWrites" />, never waits on an
/// operator's read.
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
    /// Which failures of the index's write leave a record already kept as kept: the host's store failures. The record
    /// is the truth, the index is put right at the next read, and the player or operator is answered as kept.
    /// </summary>
    public Func<Exception, bool> Passes { get; init; } = _ => false;

    /// <summary>Where a failure that passed is said, one line each: the host's console.</summary>
    public Action<string> Said { get; init; } = _ => { };

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

    /// <summary>
    /// Places the feedback in the index as its record is kept now, read again at each try, so a note never writes an
    /// older state over a newer one; none kept, its id leaves the index, and the listing says at the next read what it is.
    /// </summary>
    public Task NoteAsync(string id, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, async content => Placed(content, id, await _records.FindAsync(id, cancellationToken) is { } kept ? FeedbackSummary.Of(kept) : null), cancellationToken);

    /// <summary>Takes the id out of the index, so a save cut short leaves it missing: what the listing puts back.</summary>
    private Task ForgetAsync(string id, CancellationToken cancellationToken) =>
        ChangeAsync(id, content => Task.FromResult(Placed(content, id, null)), cancellationToken);

    /// <summary>
    /// Reads the index, changes it and writes it over the version read, reading again while another write comes first.
    /// A change that leaves the index as it was writes nothing. Every try lost, it throws: before a save, nothing was
    /// written; after it, <see cref="NotedAsync" /> says so and the id the save took out is put back by the listing.
    /// </summary>
    private async Task ChangeAsync(string id, Func<FeedbackIndexContent, Task<FeedbackIndexContent>> change, CancellationToken cancellationToken)
    {
        for (var tried = 0; tried < Tries; tried++)
        {
            var read = await _kept.ReadIndexAsync(cancellationToken);
            var content = read.Content ?? FeedbackIndexContent.Empty;
            var changed = await change(content);
            if (read.Content is not null && Same(content, changed))
            {
                return;
            }

            if (await _kept.WriteIndexAsync(changed, read.Version, cancellationToken))
            {
                return;
            }
        }

        throw new IOException($"The index of feedback lost {Tries} writes in a row to others while placing {id}.");
    }

    /// <summary>Places the feedback after its record was kept: a failure the host passes is said, and the record stays kept.</summary>
    private async Task NotedAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            await NoteAsync(id, cancellationToken);
        }
        catch (Exception failure) when (failure is IOException || Passes(failure))
        {
            Said($"  The index of feedback could not place {id} ({failure.Message}): the next read puts it back.");
        }
    }

    private static bool Same(FeedbackIndexContent one, FeedbackIndexContent other) =>
        one.Rows.SequenceEqual(other.Rows) && one.Closed.SequenceEqual(other.Closed, StringComparer.Ordinal);

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

    /// <summary>
    /// The store as the use cases are given it: each save takes the id out of the index, writes the record, then places
    /// it; each delete writes the record's going, then takes the id out.
    /// </summary>
    private sealed class Indexed(IFeedbackStore inner, FeedbackIndex index) : IFeedbackStore
    {
        public async Task SaveAsync(Feedback feedback, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(feedback);
            await index.ForgetAsync(feedback.Id, cancellationToken);
            await inner.SaveAsync(feedback, cancellationToken);
            await index.NotedAsync(feedback.Id, cancellationToken);
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            await inner.DeleteAsync(id, cancellationToken);
            await index.NotedAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default) => inner.IdsAsync(cancellationToken);

        public Task<IReadOnlyList<string>> IdsSentAsync(DateTimeOffset after, DateTimeOffset until, CancellationToken cancellationToken = default) =>
            inner.IdsSentAsync(after, until, cancellationToken);

        public Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default) => inner.FindAsync(id, cancellationToken);
    }
}
