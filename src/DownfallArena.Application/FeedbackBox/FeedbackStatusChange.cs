namespace DownfallArena.Application.FeedbackBox;

/// <summary>Feedback as the operator left it, and the mute its sender earned when that made the third spam mark in 30 days.</summary>
public sealed record FeedbackStatusChange(Feedback Feedback, FeedbackMute? Muted);
