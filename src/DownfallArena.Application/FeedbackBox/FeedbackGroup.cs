namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// An idea as its sheet reads it (docs/tabletop/feedback-plan.md, I1): the ideas merged into it, oldest first, and how
/// many asked; or, for one merged into another, <see cref="Into" />, the idea its group became.
/// </summary>
public sealed record FeedbackGroup(Feedback? Into, IReadOnlyList<Feedback> Merged, FeedbackAsks Asks);
