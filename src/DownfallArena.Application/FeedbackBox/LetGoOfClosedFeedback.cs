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
        foreach (var id in await store.IdsAsync(cancellationToken))
        {
            // Sent within the 180 days, nothing is due: a merged idea among it joined one open then, so closed after it.
            if (Feedback.SentAt(id) is { } at && now - at <= Feedback.KeptClosed)
            {
                continue;
            }

            // A merged idea follows the idea its group became; a chain that ends nowhere means that one is gone.
            var feedback = await store.FindAsync(id, cancellationToken);
            var deciding = feedback is { MergedInto: not null } ? await FeedbackGroups.RootAsync(store, feedback, cancellationToken) : feedback;
            if (feedback is not null && (deciding is null || (deciding is { IsClosed: true, ClosedAt: { } closed } && now - closed > Feedback.KeptClosed)))
            {
                await store.DeleteAsync(id, cancellationToken);
                gone++;
            }
        }

        return gone;
    }
}
