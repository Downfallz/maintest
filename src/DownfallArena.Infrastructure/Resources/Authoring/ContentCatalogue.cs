namespace DownfallArena.Infrastructure.Resources.Authoring;

/// <summary>
/// Everything authored under one content directory, plus what the data builder makes of it: the content hash
/// when it builds, the problems when it does not, and the notes about what the <c>enabled</c> switch left out.
/// </summary>
public sealed record ContentCatalogue
{
    public required string Directory { get; init; }

    public IReadOnlyList<ContentDocument> Creatures { get; init; } = [];

    public IReadOnlyList<ContentDocument> Spells { get; init; } = [];

    public IReadOnlyList<ContentDocument> TalentTrees { get; init; } = [];

    public IReadOnlyDictionary<string, string> Aliases { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The content hash of the last successful build, or <c>null</c> when the content does not build.</summary>
    public string? ContentHash { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}
