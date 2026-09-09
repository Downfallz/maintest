using System.Text.Json;

namespace DownfallArena.Infrastructure.Resources.Authoring;

/// <summary>
/// One authored file as the studio sees it: where it lives, what it says about itself, and its raw JSON, so a
/// page can render a form without a second schema (ADR 0015). <see cref="Problem"/> is set when the file does
/// not parse into its DTO; the document is still listed, so the author can fix it.
/// </summary>
public sealed record ContentDocument
{
    public required ContentKind Kind { get; init; }

    /// <summary>Path relative to the content directory, with forward slashes (e.g. <c>Spells/brawler/pummel.v1.json</c>).</summary>
    public required string Path { get; init; }

    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>False only when the file says <c>"enabled": false</c>.</summary>
    public required bool Enabled { get; init; }

    public required JsonElement Document { get; init; }

    public string? Problem { get; init; }
}
