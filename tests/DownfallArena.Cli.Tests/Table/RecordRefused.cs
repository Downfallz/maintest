using DownfallArena.Application.Learning.Ports;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A store whose writes of <c>table.json</c> fail while <see cref="Refusing" /> is set, as a storage outage fails a
/// seat change, and whose reads of it fail while <see cref="ReadsRefused" /> is; or of another document, when named.
/// </summary>
internal sealed class RecordRefused(IArtifactStore inner, string? file = null) : IArtifactStore
{
    public string File { get; } = file ?? TableRecord.File;

    public bool Refusing { get; set; }

    public bool ReadsRefused { get; set; }

    /// <summary>What a refused read or write throws: an outage's <see cref="IOException" /> unless given.</summary>
    public Func<Exception> Failure { get; init; } = () => new IOException("The store is out.");

    public IArtifactWriter Writer(string run) => new Refuser(this, inner.Writer(run));

    public IArtifactReader Reader(string run) => new Unread(this, inner.Reader(run));

    public string LocationOf(string run) => inner.LocationOf(run);

    public Task<IReadOnlyList<string>> RunsAsync(CancellationToken cancellationToken = default) => inner.RunsAsync(cancellationToken);

    public Task<bool> DeleteAsync(string run, CancellationToken cancellationToken = default) => inner.DeleteAsync(run, cancellationToken);

    private sealed class Unread(RecordRefused store, IArtifactReader inner) : IArtifactReader
    {
        public Task<IReadOnlyList<string>> ListAsync(string relativePrefix, CancellationToken cancellationToken = default) =>
            inner.ListAsync(relativePrefix, cancellationToken);

        public Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default) =>
            Refused(relativePath) ? Task.FromException<string?>(store.Failure()) : inner.ReadTextAsync(relativePath, cancellationToken);

        public Task<TaggedText?> ReadTaggedAsync(string relativePath, CancellationToken cancellationToken = default) =>
            Refused(relativePath) ? Task.FromException<TaggedText?>(store.Failure()) : inner.ReadTaggedAsync(relativePath, cancellationToken);

        private bool Refused(string relativePath) => store.ReadsRefused && relativePath.EndsWith(store.File, StringComparison.Ordinal);
    }

    private sealed class Refuser(RecordRefused store, IArtifactWriter inner) : IArtifactWriter
    {
        public Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default) =>
            store.Refusing && relativePath.EndsWith(store.File, StringComparison.Ordinal)
                ? Task.FromException(store.Failure())
                : inner.WriteJsonAsync(relativePath, value, cancellationToken);

        public Task<string?> WriteJsonIfAsync<TValue>(string relativePath, TValue value, string? expected, CancellationToken cancellationToken = default) =>
            store.Refusing && relativePath.EndsWith(store.File, StringComparison.Ordinal)
                ? Task.FromException<string?>(store.Failure())
                : inner.WriteJsonIfAsync(relativePath, value, expected, cancellationToken);

        public Task<bool> DeleteIfAsync(string relativePath, string expected, CancellationToken cancellationToken = default) =>
            inner.DeleteIfAsync(relativePath, expected, cancellationToken);

        public Task StartJsonLinesAsync(string relativePath, CancellationToken cancellationToken = default) =>
            inner.StartJsonLinesAsync(relativePath, cancellationToken);

        public Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default) =>
            inner.AppendJsonLinesAsync(relativePath, values, cancellationToken);
    }
}
