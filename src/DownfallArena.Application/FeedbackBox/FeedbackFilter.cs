namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// What the Feedback tab lists (docs/tabletop/feedback-plan.md): a kind, a status or only what is open, a category, and
/// only what was filed as an issue (R5), whatever its status since; none of them is everything. An idea merged into
/// another is never listed: it is read inside the one it joined.
/// </summary>
public sealed record FeedbackFilter(FeedbackKind? Kind = null, FeedbackStatus? Status = null, string? Category = null, bool OpenOnly = false, bool FiledOnly = false)
{
    /// <summary>Whether it keeps open feedback alone, which the index of feedback lists without opening a record (#866).</summary>
    public bool KeepsOpenOnly => OpenOnly || Status is FeedbackStatus.New or FeedbackStatus.Seen or FeedbackStatus.Reproduced or FeedbackStatus.Filed or FeedbackStatus.Planned;

    public bool Keeps(Feedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        return Keeps(FeedbackSummary.Of(feedback));
    }

    public bool Keeps(FeedbackSummary feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        return feedback.MergedInto is null
            && (Kind is null || feedback.Kind == Kind)
            && (Status is null || feedback.Status == Status)
            && (Category is null || feedback.Category == Category)
            && (!OpenOnly || !feedback.IsClosed)
            && (!FiledOnly || feedback.Issue is not null);
    }
}
