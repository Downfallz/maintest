using DownfallArena.Application.Learning.Ports;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A store whose writes of <c>table.json</c> fail while <see cref="Refusing" /> is set, as a storage outage fails a
/// seat change, and whose reads of it fail while <see cref="ReadsRefused" /> is.
/// </summary>
internal sealed class RecordRefused(IArtifactStore inner) : IArtifactStore
{
    public bool Refusing { get; set; }

    public bool ReadsRefused { get; set; }

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
            Refused(relativePath) ? Task.FromException<string?>(new IOException("The store is out.")) : inner.ReadTextAsync(relativePath, cancellationToken);

        public Task<TaggedText?> ReadTaggedAsync(string relativePath, CancellationToken cancellationToken = default) =>
            Refused(relativePath) ? Task.FromException<TaggedText?>(new IOException("The store is out.")) : inner.ReadTaggedAsync(relativePath, cancellationToken);

        private bool Refused(string relativePath) => store.ReadsRefused && relativePath.EndsWith(TableRecord.File, StringComparison.Ordinal);
    }

    private sealed class Refuser(RecordRefused store, IArtifactWriter inner) : IArtifactWriter
    {
        public Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default) =>
            store.Refusing && relativePath.EndsWith(TableRecord.File, StringComparison.Ordinal)
                ? Task.FromException(new IOException("The store is out."))
                : inner.WriteJsonAsync(relativePath, value, cancellationToken);

        public Task<string?> WriteJsonIfAsync<TValue>(string relativePath, TValue value, string? expected, CancellationToken cancellationToken = default) =>
            store.Refusing && relativePath.EndsWith(TableRecord.File, StringComparison.Ordinal)
                ? Task.FromException<string?>(new IOException("The store is out."))
                : inner.WriteJsonIfAsync(relativePath, value, expected, cancellationToken);

        public Task StartJsonLinesAsync(string relativePath, CancellationToken cancellationToken = default) =>
            inner.StartJsonLinesAsync(relativePath, cancellationToken);

        public Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default) =>
            inner.AppendJsonLinesAsync(relativePath, values, cancellationToken);
    }
}
