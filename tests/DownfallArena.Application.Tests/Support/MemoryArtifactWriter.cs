using DownfallArena.Application.Learning.Ports;

namespace DownfallArena.Application.Tests.Support;

/// <summary>
/// An artifact writer that keeps documents and lines in memory, keyed by their relative path.
/// </summary>
internal sealed class MemoryArtifactWriter : IArtifactWriter
{
    public Dictionary<string, object> Documents { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, List<object>> Lines { get; } = new(StringComparer.Ordinal);

    /// <summary>The paths written, in order, one per call.</summary>
    public List<string> Writes { get; } = [];

    public Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default)
    {
        Documents[relativePath] = value ?? throw new ArgumentNullException(nameof(value));
        Writes.Add(relativePath);
        return Task.CompletedTask;
    }

    public Task<string?> WriteJsonIfAsync<TValue>(string relativePath, TValue value, string? expected, CancellationToken cancellationToken = default)
    {
        var current = Documents.TryGetValue(relativePath, out var document) ? Tag(document) : null;
        if (current != expected)
        {
            return Task.FromResult<string?>(null);
        }

        Documents[relativePath] = value ?? throw new ArgumentNullException(nameof(value));
        Writes.Add(relativePath);
        return Task.FromResult<string?>(Tag(value));
    }

    private static string Tag(object document) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(document).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public Task StartJsonLinesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        Lines[relativePath] = [];
        Writes.Add(relativePath);
        return Task.CompletedTask;
    }

    public Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default)
    {
        if (!Lines.TryGetValue(relativePath, out var lines))
        {
            lines = [];
            Lines[relativePath] = lines;
        }

        lines.AddRange(values.Cast<object>());
        Writes.Add(relativePath);
        return Task.CompletedTask;
    }

    public TValue Document<TValue>(string relativePath) => (TValue)Documents[relativePath];

    public IReadOnlyList<TValue> LinesOf<TValue>(string relativePath) =>
        Lines.TryGetValue(relativePath, out var lines) ? [.. lines.Cast<TValue>()] : [];
}
