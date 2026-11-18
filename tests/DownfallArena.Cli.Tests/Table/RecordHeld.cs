using DownfallArena.Application.Learning.Ports;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>A store whose writes of the table's record wait, once held, until they are let go: a record write still on its way.</summary>
internal sealed class RecordHeld(IArtifactStore inner) : IArtifactStore
{
    private TaskCompletionSource? _held;

    /// <summary>The next writes of the record wait until <see cref="Release" />.</summary>
    public void Hold() => _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _held?.TrySetResult();

    public IArtifactWriter Writer(string run) => new Waiter(this, inner.Writer(run));

    public IArtifactReader Reader(string run) => inner.Reader(run);

    public string LocationOf(string run) => inner.LocationOf(run);

    public Task<IReadOnlyList<string>> RunsAsync(CancellationToken cancellationToken = default) => inner.RunsAsync(cancellationToken);

    public Task<bool> DeleteAsync(string run, CancellationToken cancellationToken = default) => inner.DeleteAsync(run, cancellationToken);

    private Task Wait(string relativePath) =>
        _held is { } held && relativePath.EndsWith(TableRecord.File, StringComparison.Ordinal) ? held.Task : Task.CompletedTask;

    private sealed class Waiter(RecordHeld store, IArtifactWriter inner) : IArtifactWriter
    {
        public async Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default)
        {
            await store.Wait(relativePath);
            await inner.WriteJsonAsync(relativePath, value, cancellationToken);
        }

        public async Task<string?> WriteJsonIfAsync<TValue>(string relativePath, TValue value, string? expected, CancellationToken cancellationToken = default)
        {
            await store.Wait(relativePath);
            return await inner.WriteJsonIfAsync(relativePath, value, expected, cancellationToken);
        }

        public Task<bool> DeleteIfAsync(string relativePath, string expected, CancellationToken cancellationToken = default) =>
            inner.DeleteIfAsync(relativePath, expected, cancellationToken);

        public Task StartJsonLinesAsync(string relativePath, CancellationToken cancellationToken = default) =>
            inner.StartJsonLinesAsync(relativePath, cancellationToken);

        public Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default) =>
            inner.AppendJsonLinesAsync(relativePath, values, cancellationToken);
    }
}
