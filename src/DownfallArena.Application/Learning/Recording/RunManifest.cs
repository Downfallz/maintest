namespace DownfallArena.Application.Learning.Recording;

/// <summary>
/// What a run directory holds and where it came from: the run stamp, the feature schema the steps use, and the
/// counts. Written when the run starts (zero counts) and again when it finishes.
/// </summary>
public sealed record RunManifest
{
    public required RunStamp Stamp { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required string SchemaId { get; init; }

    public required string SchemaVersion { get; init; }

    public required IReadOnlyList<string> FeatureNames { get; init; }

    public required int Matches { get; init; }

    public required int Steps { get; init; }

    public required int Episodes { get; init; }

    public required bool Traces { get; init; }
}
