namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// An idea as the operator unmerged it, the group's idea it left (<see cref="From" />), and whether that changed anything:
/// asked again, or of an idea never merged, it does not, and leaves none.
/// </summary>
public sealed record FeedbackUnmerge(Feedback Feedback, string? From, bool Changed);
