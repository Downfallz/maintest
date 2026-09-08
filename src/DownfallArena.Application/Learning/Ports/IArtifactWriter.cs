namespace DownfallArena.Application.Learning.Ports;

/// <summary>
/// Where learning artifacts go (ADR 0013): JSON documents and JSON lines files, addressed by a path relative to
/// the run. Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IArtifactWriter
{
    /// <summary>Writes one JSON document, replacing any previous one at that path.</summary>
    Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default);

    /// <summary>Appends one JSON line per value to the file at that path, creating it when needed.</summary>
    Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default);
}
