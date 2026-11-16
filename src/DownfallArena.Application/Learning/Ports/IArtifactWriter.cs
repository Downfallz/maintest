namespace DownfallArena.Application.Learning.Ports;

/// <summary>
/// Where learning artifacts go (ADR 0013): JSON documents and JSON lines files, addressed by a path relative to
/// the run. Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IArtifactWriter
{
    /// <summary>Writes one JSON document, replacing any previous one at that path.</summary>
    Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a document only if it is still the content read under <paramref name="expected" /> -- or, with
    /// none, only if there is no document yet. Two writers that read the same content cannot both write after
    /// it: the second is refused and reads again.
    /// </summary>
    /// <returns>The tag of what was written, or none when the document had moved.</returns>
    Task<string?> WriteJsonIfAsync<TValue>(string relativePath, TValue value, string? expected, CancellationToken cancellationToken = default);

    /// <summary>Creates the JSON lines file at that path empty, replacing any previous one.</summary>
    Task StartJsonLinesAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Appends one JSON line per value to the file at that path, creating it when needed.</summary>
    Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default);
}
