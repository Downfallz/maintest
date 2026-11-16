namespace DownfallArena.Cli.Table;

/// <summary>
/// What a page posts to <c>POST /api/feedback</c> (ADR 0189): a bug report
/// <c>{"kind":"bug","category","happened","expected?","attach","page","errors"}</c> or an idea
/// <c>{"kind":"idea","where","problem","wish?","page"}</c>.
/// </summary>
internal sealed record FeedbackBody
{
    public string? Kind { get; init; }

    public string? Category { get; init; }

    public string? Happened { get; init; }

    public string? Expected { get; init; }

    public bool Attach { get; init; }

    public string? Where { get; init; }

    public string? Problem { get; init; }

    public string? Wish { get; init; }

    public string? Page { get; init; }

    public IReadOnlyList<FeedbackBodyError>? Errors { get; init; }
}
