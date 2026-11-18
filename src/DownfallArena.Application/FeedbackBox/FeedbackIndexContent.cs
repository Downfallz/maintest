namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The index of feedback as a store keeps it (#866): a row for each piece the operator's reads need without its record
/// (<see cref="FeedbackSummary.HeldAt" />), and the ids of the rest, so a listing of the store tells what it lacks.
/// </summary>
public sealed record FeedbackIndexContent(IReadOnlyList<FeedbackSummary> Rows, IReadOnlyList<string> Closed)
{
    public static readonly FeedbackIndexContent Empty = new([], []);
}
