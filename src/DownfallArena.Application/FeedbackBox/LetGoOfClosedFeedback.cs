using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The tending pass's share of feedback (ADR 0189): what was closed more than <see cref="Feedback.KeptClosed" /> ago is
/// deleted. Open feedback is kept however old it is: nobody has answered it yet. An idea merged into another goes with
/// the idea its group became, or once that one is gone.
/// </summary>
public sealed class LetGoOfClosedFeedback(IFeedbackStore store, TimeProvider clock)
{
    /// <summary>Deletes what was closed long enough ago, and answers how many went.</summary>
    public async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var gone = 0;
        var ids = await store.IdsAsync(cancellationToken);
        var listed = ids.ToHashSet(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            // Sent within the 180 days, nothing is due: a merged idea among it joined one open then, so closed after it.
            if (Feedback.SentAt(id) is { } at && now - at <= Feedback.KeptClosed)
            {
                continue;
            }

            if (await store.FindAsync(id, cancellationToken) is not { } feedback || !await DueAsync(feedback, listed, now, cancellationToken))
            {
                continue;
            }

            await store.DeleteAsync(id, cancellationToken);
            listed.Remove(id);
            gone++;
        }

        return gone;
    }

    /// <summary>
    /// Whether it is due: closed long enough ago, or for a merged idea, the group's idea it joined closed long enough
    /// ago, or no longer listed. One listed but unreadable is not gone, so what joined it waits.
    /// </summary>
    private async Task<bool> DueAsync(Feedback feedback, HashSet<string> listed, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (feedback.MergedInto is { } joined)
        {
            if (!listed.Contains(joined))
            {
                return true;
            }

            if (await store.FindAsync(joined, cancellationToken) is not { } group)
            {
                return false;
            }

            return Closed(group, now);
        }

        return Closed(feedback, now);
    }

    private static bool Closed(Feedback feedback, DateTimeOffset now) =>
        feedback is { IsClosed: true, ClosedAt: { } closed } && now - closed > Feedback.KeptClosed;
}
