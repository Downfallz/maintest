namespace DownfallArena.Application.FeedbackBox;

/// <summary>Feedback as the operator left it, and the kind it was before; none when it already was the kind asked.</summary>
public sealed record FeedbackKindChange(Feedback Feedback, FeedbackKind? Was);
