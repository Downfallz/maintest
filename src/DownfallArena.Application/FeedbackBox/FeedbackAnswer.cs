using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// What <see cref="SendFeedback" /> answers: the id kept, and whether the text was merged into feedback the sender
/// had already sent; or why nothing was kept, with how long to wait when waiting helps.
/// </summary>
public sealed record FeedbackAnswer(string? Id, bool Merged, DomainError? Refused, TimeSpan? RetryAfter = null)
{
    public static FeedbackAnswer Kept(string id) => new(id, Merged: false, Refused: null);

    public static FeedbackAnswer MergedInto(string id) => new(id, Merged: true, Refused: null);

    public static FeedbackAnswer Refuse(DomainError error, TimeSpan? retryAfter = null) => new(null, Merged: false, error, retryAfter);
}
