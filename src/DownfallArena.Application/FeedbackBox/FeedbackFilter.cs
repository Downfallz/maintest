namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// What the Feedback tab lists (docs/tabletop/feedback-plan.md): a kind, a status or only what is open, a category; none
/// of them is everything. An idea merged into another is never listed: it is read inside the one it joined.
/// </summary>
public sealed record FeedbackFilter(FeedbackKind? Kind = null, FeedbackStatus? Status = null, string? Category = null, bool OpenOnly = false)
{
    public bool Keeps(Feedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        return feedback.MergedInto is null
            && (Kind is null || feedback.Kind == Kind)
            && (Status is null || feedback.Status == Status)
            && (Category is null || feedback.Category == Category)
            && (!OpenOnly || !feedback.IsClosed);
    }
}
