using System.Text.Json;

namespace DownfallArena.Infrastructure.Resources.Authoring;

/// <summary>
/// Everything authored under one content directory, plus what the data builder makes of it: the content hash
/// when it builds, the problems when it does not, and the notes about what the <c>enabled</c> switch left out.
/// <para>
/// A class rather than a record: it is read once and serialised, never copied or compared, so value equality
/// would only be weight nothing exercises.
/// </para>
/// </summary>
public sealed class ContentCatalogue
{
    public required string Directory { get; init; }

    public IReadOnlyList<ContentDocument> Creatures { get; init; } = [];

    public IReadOnlyList<ContentDocument> Spells { get; init; } = [];

    public IReadOnlyList<ContentDocument> TalentTrees { get; init; } = [];

    public IReadOnlyDictionary<string, string> Aliases { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The balance knobs as authored, passed through whole and never interpreted here (<c>data/balance/README.md</c>).
    /// <para>
    /// It says what a tuning pass may move about each spell and what each spell is for, which is authoring
    /// metadata rather than content: the data builder never reads it, so carrying it cannot move the content
    /// hash. The studio reads it to show a knob beside the number it governs, and <c>check-knobs</c> stays the
    /// authority on whether it agrees with the content.
    /// </para>
    /// <para>
    /// <c>null</c> when the directory has no knobs file, or when the one it has does not parse — the page says
    /// which rather than drawing an empty sheet, and <see cref="Notes"/> carries the reason for the second.
    /// </para>
    /// </summary>
    public JsonElement? Balance { get; init; }

    /// <summary>The content hash of the last successful build, or <c>null</c> when the content does not build.</summary>
    public string? ContentHash { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}
