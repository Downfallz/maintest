namespace DownfallArena.Application.FeedbackBox;

/// <summary>What a sender wrote again on the same position, or about the same idea within the hour: added to the first, never a row of its own.</summary>
public sealed record FeedbackAddition(DateTimeOffset At, string Text, string? Expected);
