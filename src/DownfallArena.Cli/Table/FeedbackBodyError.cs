namespace DownfallArena.Cli.Table;

/// <summary>One error the page caught, as it posts it: the message and the file:line.</summary>
internal sealed record FeedbackBodyError(string? Message, string? Where);
