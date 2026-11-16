namespace DownfallArena.Application.FeedbackBox;

/// <summary>An idea as the operator merged it, the idea it joined, and whether that changed anything: merged again, it does not.</summary>
public sealed record FeedbackMerge(Feedback Feedback, Feedback Into, bool Changed);
