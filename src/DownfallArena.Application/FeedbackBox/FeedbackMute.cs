namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// A sender stopped from sending feedback (docs/tabletop/feedback-plan.md, "Against spam"): by the operator from a
/// sheet, or on its own once three of the sender's were dismissed as spam within <see cref="SpamWindow" />. It ends on
/// its own <see cref="Lasts" /> after it began, or when the operator lifts it.
/// </summary>
/// <param name="Why"><see cref="ByOperator" /> or <see cref="BySpam" />.</param>
public sealed record FeedbackMute(DateTimeOffset At, DateTimeOffset Until, string By, string Why)
{
    /// <summary>How long a mute lasts before it ends on its own.</summary>
    public static readonly TimeSpan Lasts = TimeSpan.FromDays(30);

    /// <summary>How far back the spam marks that mute a sender are counted.</summary>
    public static readonly TimeSpan SpamWindow = TimeSpan.FromDays(30);

    /// <summary>How many spam marks within <see cref="SpamWindow" /> mute a sender.</summary>
    public const int SpamMarks = 3;

    public const string ByOperator = "operator";

    public const string BySpam = "spam";

    /// <summary>A mute begun now by an operator, for the reason given.</summary>
    public static FeedbackMute Begun(DateTimeOffset now, string by, string why) => new(now, now + Lasts, by, why);

    /// <summary>Whether it still holds at that time.</summary>
    public bool HoldsAt(DateTimeOffset now) => now < Until;
}
