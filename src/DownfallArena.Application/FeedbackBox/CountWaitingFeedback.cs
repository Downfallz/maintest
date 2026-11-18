using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>What the Overview's feedback card counts (docs/tabletop/feedback-plan.md, R3).</summary>
public sealed class CountWaitingFeedback(IFeedbackStore store, TimeProvider clock)
{
    /// <summary>
    /// The feedback nobody has looked at, the bug reports among it, and what was sent in the last day against the host's
    /// ceiling: that last count lists the day alone (#693).
    /// </summary>
    public async Task<FeedbackWaiting> HandleAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var lastDay = (await store.IdsSentAsync(now - SendFeedback.Day, now, cancellationToken)).Count;
        var waiting = (await FeedbackRecords.AllAsync(store, cancellationToken)).Where(one => one.Status == FeedbackStatus.New).ToList();
        return new FeedbackWaiting(waiting.Count, waiting.Count(one => one.Kind == FeedbackKind.Bug), lastDay, lastDay >= SendFeedback.PerHost);
    }
}
