using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>One piece of feedback, as the operator's sheet and its actions find it (ADR 0189).</summary>
public sealed class FindFeedback(IFeedbackStore store)
{
    /// <summary>The feedback by its id, or none when none is kept under it or the id is not one feedback could have.</summary>
    public Task<Feedback?> HandleAsync(string id, CancellationToken cancellationToken = default) =>
        FeedbackRecords.FindAsync(store, id, cancellationToken);
}
