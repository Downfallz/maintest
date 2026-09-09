using System.Text.Json;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Cli.Studio;

/// <summary>One document the page wants written: which kind of content it is, its file, and the document itself.</summary>
internal sealed record SaveDocumentRequest
{
    /// <summary>Nullable because <c>Creature</c> is the enum's zero: a missing kind has to be refused, not assumed.</summary>
    public ContentKind? Kind { get; init; }

    public string Path { get; init; } = string.Empty;

    public JsonElement Document { get; init; }
}
