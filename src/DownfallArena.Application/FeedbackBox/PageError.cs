namespace DownfallArena.Application.FeedbackBox;

/// <summary>One error the page caught before its player wrote (an <c>error</c> or <c>unhandledrejection</c>): its message and its file:line.</summary>
public sealed record PageError(string Message, string? Where);
