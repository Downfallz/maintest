using DownfallArena.Application.Learning.Ports;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>A store whose writes of <c>table.json</c> fail while <see cref="Refusing" /> is set, as a storage outage fails a seat change.</summary>
internal sealed class RecordRefused(IArtifactStore inner) : IArtifactStore
{
    public bool Refusing { get; set; }

    public IArtifactWriter Writer(string run) => new Refuser(this, inner.Writer(run));

    public IArtifactReader Reader(string run) => inner.Reader(run);

    public string LocationOf(string run) => inner.LocationOf(run);

    public Task<IReadOnlyList<string>> RunsAsync(CancellationToken cancellationToken = default) => inner.RunsAsync(cancellationToken);

    public Task<bool> DeleteAsync(string run, CancellationToken cancellationToken = default) => inner.DeleteAsync(run, cancellationToken);

    private sealed class Refuser(RecordRefused store, IArtifactWriter inner) : IArtifactWriter
    {
        public Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default) =>
            store.Refusing && relativePath.EndsWith(TableRecord.File, StringComparison.Ordinal)
                ? Task.FromException(new IOException("The store is out."))
                : inner.WriteJsonAsync(relativePath, value, cancellationToken);

        public Task StartJsonLinesAsync(string relativePath, CancellationToken cancellationToken = default) =>
            inner.StartJsonLinesAsync(relativePath, cancellationToken);

        public Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default) =>
            inner.AppendJsonLinesAsync(relativePath, values, cancellationToken);
    }
}
