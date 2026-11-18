namespace DownfallArena.Application.FeedbackBox;

/// <summary>What the Overview's feedback card counts (docs/tabletop/feedback-plan.md, R3).</summary>
public sealed class CountWaitingFeedback(FeedbackIndex index, TimeProvider clock)
{
    /// <summary>
    /// The feedback nobody has looked at, the bug reports among it, read from the index of open feedback (#866), and what
    /// was sent in the last day against the host's ceiling: that last count lists the day alone (#693).
    /// </summary>
    public async Task<FeedbackWaiting> HandleAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var lastDay = (await index.Records.IdsSentAsync(now - SendFeedback.Day, now + SendFeedback.Ahead, cancellationToken)).Count;
        var waiting = (await index.RowsAsync(cancellationToken)).Where(one => one.Status == FeedbackStatus.New).ToList();
        return new FeedbackWaiting(waiting.Count, waiting.Count(one => one.Kind == FeedbackKind.Bug), lastDay, lastDay >= SendFeedback.PerHost);
    }
}
