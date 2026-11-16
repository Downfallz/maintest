namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// Where a piece of feedback stands (docs/tabletop/feedback-plan.md): new when sent, then what the operator makes of
/// it. Fixed, shipped, declined and dismissed close it, and a closed one is kept <see cref="Feedback.KeptClosed" />.
/// </summary>
public enum FeedbackStatus
{
    New,
    Seen,
    Reproduced,
    Filed,
    Fixed,
    Dismissed,
    Planned,
    Shipped,
    Declined,
}
