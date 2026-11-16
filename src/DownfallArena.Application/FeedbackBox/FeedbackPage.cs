namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// A page of the Feedback tab, counted from one, and how many the filter kept in all: newest first, or for ideas alone
/// by how many asked. <see cref="Asks" /> says how many asked each idea on the page, by its id.
/// </summary>
public sealed record FeedbackPage(IReadOnlyList<Feedback> Rows, int Total, int Page, int Pages, IReadOnlyDictionary<string, FeedbackAsks> Asks);
