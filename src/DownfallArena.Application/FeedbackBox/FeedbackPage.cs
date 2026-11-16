namespace DownfallArena.Application.FeedbackBox;

/// <summary>A page of the Feedback tab, newest first, counted from one, and how many the filter kept in all.</summary>
public sealed record FeedbackPage(IReadOnlyList<Feedback> Rows, int Total, int Page, int Pages);
