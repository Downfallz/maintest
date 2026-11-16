namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// How many players asked an idea, the ideas merged into it counted (docs/tabletop/feedback-plan.md, I1): each sender
/// once, every deleted account apart; when the first of them asked, and when the last wrote.
/// </summary>
public sealed record FeedbackAsks(int AskedBy, DateTimeOffset Since, DateTimeOffset Latest);
