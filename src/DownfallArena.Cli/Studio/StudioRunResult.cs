namespace DownfallArena.Cli.Studio;

/// <summary>
/// Where a run landed: its id, the page that shows it, the content it played, and the files it wrote. One of
/// these is written into the run's directory as <c>run.json</c>, which is what the run list reads back.
/// </summary>
internal sealed record StudioRunResult
{
    public required string Id { get; init; }

    public required string Url { get; init; }

    public required string Mode { get; init; }

    public required int Seed { get; init; }

    public required string Player1 { get; init; }

    public required string Player2 { get; init; }

    /// <summary>How many seeds an evaluation played; one for a match.</summary>
    public required int Matches { get; init; }

    /// <summary>The content hash the run played, so the list says which runs are comparable.</summary>
    public required string ContentHash { get; init; }

    public required DateTimeOffset At { get; init; }

    public required string Directory { get; init; }

    public IReadOnlyList<string> Files { get; init; } = [];
}
