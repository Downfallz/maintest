using DownfallArena.Application.Accounts;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// What the index of feedback keeps of one piece (#866): what the Feedback tab filters and sorts on, how many asked an
/// idea, the Overview's count and the spam count read, and nothing a player wrote.
/// </summary>
public sealed record FeedbackSummary(
    string Id,
    FeedbackKind Kind,
    FeedbackStatus Status,
    string Category,
    string Sender,
    DateTimeOffset At,
    DateTimeOffset LastWritten,
    string? MergedInto = null,
    DateTimeOffset? ClosedAt = null,
    bool Spam = false,
    int? Issue = null)
{
    /// <summary>Whether the operator closed it: fixed, shipped, declined or dismissed.</summary>
    public bool IsClosed => Status is FeedbackStatus.Fixed or FeedbackStatus.Shipped or FeedbackStatus.Declined or FeedbackStatus.Dismissed;

    /// <summary>
    /// Whether the index keeps it as a row, read without its record: while it is open, and a spam mark for the window it
    /// counts towards its sender's mute. Any other is kept by its id alone.
    /// </summary>
    public bool HeldAt(DateTimeOffset now) => !IsClosed || (Spam && ClosedAt is { } closed && now - closed <= FeedbackMute.SpamWindow);

    public static FeedbackSummary Of(Feedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        return new(feedback.Id, feedback.Kind, feedback.Status, feedback.Category, feedback.Sender, feedback.At, feedback.LastWritten, feedback.MergedInto, feedback.ClosedAt, feedback.IsSpam, feedback.Issue);
    }
}
