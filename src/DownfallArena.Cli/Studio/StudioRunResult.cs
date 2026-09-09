namespace DownfallArena.Cli.Studio;

/// <summary>Where a run landed: its id, the page that shows it, and the files it wrote.</summary>
internal sealed record StudioRunResult
{
    public required string Id { get; init; }

    public required string Url { get; init; }

    public required string Mode { get; init; }

    public required int Seed { get; init; }

    public required string Player1 { get; init; }

    public required string Player2 { get; init; }

    public required string Directory { get; init; }

    public IReadOnlyList<string> Files { get; init; } = [];
}
