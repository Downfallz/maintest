namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The Overview's feedback card (docs/tabletop/feedback-plan.md): what nobody has looked at yet, the bug reports among
/// it, what was sent in the last day, and whether the host has reached its ceiling for the day.
/// </summary>
public sealed record FeedbackWaiting(int New, int NewBugs, int LastDay, bool Full);
