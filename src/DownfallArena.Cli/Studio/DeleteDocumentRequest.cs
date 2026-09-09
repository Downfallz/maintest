using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Studio;

/// <summary>One document the page wants removed.</summary>
internal sealed record DeleteDocumentRequest
{
    /// <summary>Nullable because <c>Creature</c> is the enum's zero: a missing kind has to be refused, not assumed.</summary>
    public ContentKind? Kind { get; init; }

    public string Path { get; init; } = string.Empty;
}
