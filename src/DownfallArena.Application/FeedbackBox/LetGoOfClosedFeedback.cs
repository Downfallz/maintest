using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The tending pass's share of feedback (ADR 0189): what was closed more than <see cref="Feedback.KeptClosed" /> ago is
/// deleted. Open feedback is kept however old it is: nobody has answered it yet.
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
            if (Feedback.SentAt(id) is { } at && now - at <= Feedback.KeptClosed)
            {
                continue;
            }

            if (await store.FindAsync(id, cancellationToken) is { IsClosed: true, ClosedAt: { } closed } && now - closed > Feedback.KeptClosed)
            {
                await store.DeleteAsync(id, cancellationToken);
                gone++;
            }
        }

        return gone;
    }
}
