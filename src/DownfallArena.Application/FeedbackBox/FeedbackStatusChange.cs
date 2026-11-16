using DownfallArena.Application.Accounts;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// Feedback as the operator left it; whether that changed anything, since a status asked again writes nothing; and the
/// mute its sender earned when that made the third spam mark in 30 days.
/// </summary>
public sealed record FeedbackStatusChange(Feedback Feedback, bool Changed, FeedbackMute? Muted);
