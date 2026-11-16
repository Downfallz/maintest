namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// What a sender asks to be kept (docs/tabletop/feedback-plan.md, the routes): the page's fields, checked by
/// <see cref="SendFeedback" />, with who sent it and what the host attached. <see cref="Address" /> is the network
/// address a guest sent from, counted in memory and never kept.
/// </summary>
public sealed record FeedbackSent
{
    public required FeedbackKind Kind { get; init; }

    public required string Sender { get; init; }

    /// <summary>The signed-in account, or none for a guest.</summary>
    public string? Account { get; init; }

    public string? Address { get; init; }

    public required string? Category { get; init; }

    public required string? Text { get; init; }

    public string? Expected { get; init; }

    public string? Page { get; init; }

    public string? Browser { get; init; }

    public IReadOnlyList<PageError> Errors { get; init; } = [];

    public FeedbackContext? Context { get; init; }
}
