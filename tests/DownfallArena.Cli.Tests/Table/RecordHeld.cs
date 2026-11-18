using DownfallArena.Application.Learning.Ports;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A store whose writes of the table's record, or of another document when named, wait, once held, until they are let
/// go: a write still on its way.
/// </summary>
internal sealed class RecordHeld(IArtifactStore inner, string? file = null) : IArtifactStore
{
    private readonly string _file = file ?? TableRecord.File;
    private TaskCompletionSource? _held;
    private TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The next writes of the document wait until <see cref="Release" />.</summary>
    public void Hold()
    {
        _reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Completes once a write of the document is waiting on the hold.</summary>
    public Task Reached => _reached.Task;

    public void Release() => _held?.TrySetResult();

    public IArtifactWriter Writer(string run) => new Waiter(this, inner.Writer(run));

    public IArtifactReader Reader(string run) => inner.Reader(run);

    public string LocationOf(string run) => inner.LocationOf(run);

    public Task<IReadOnlyList<string>> RunsAsync(CancellationToken cancellationToken = default) => inner.RunsAsync(cancellationToken);

    public Task<bool> DeleteAsync(string run, string? first, CancellationToken cancellationToken = default) => inner.DeleteAsync(run, first, cancellationToken);

    private Task Wait(string relativePath)
    {
        if (_held is not { } held || !relativePath.EndsWith(_file, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        _reached.TrySetResult();
        return held.Task;
    }

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
