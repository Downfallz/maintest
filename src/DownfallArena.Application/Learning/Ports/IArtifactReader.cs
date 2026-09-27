namespace DownfallArena.Application.Learning.Ports;

/// <summary>
/// Reads back what an <see cref="IArtifactWriter" /> wrote for one run, by the same relative paths. Owned by
/// Application, implemented by Infrastructure: the viewer that shows a finished session reads through this
/// rather than through the filesystem, so a run kept in a blob container reads the same as one on disk
/// (ADR 0080).
/// </summary>
public interface IArtifactReader
{
    /// <summary>
    /// The relative paths under <paramref name="relativePrefix" /> (a directory name, without a trailing
    /// slash), in ordinal order; empty when nothing was written there.
    /// </summary>
    Task<IReadOnlyList<string>> ListAsync(string relativePrefix, CancellationToken cancellationToken = default);

    /// <summary>The text at that path, or <c>null</c> when nothing was written there.</summary>
    Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default);
}
