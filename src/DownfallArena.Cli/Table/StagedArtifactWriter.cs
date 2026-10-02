using DownfallArena.Application.Learning.Ports;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A writer that holds every write back until it is told to let them through, in the order they were asked,
/// and forwards them as they come once it has. For a rebuild (ADR 0091): the replay re-records the dataset as
/// it goes, but a replay can be refused, and a run refused has to be left exactly as the earlier host wrote
/// it. So nothing lands until the rebuilt match has been held against the record and kept.
/// </summary>
internal sealed class StagedArtifactWriter(IArtifactWriter inner) : IArtifactWriter
{
    private readonly Lock _gate = new();
    private readonly List<Func<CancellationToken, Task>> _held = [];
    private Task _tail = Task.CompletedTask;
    private bool _through;

    public Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default) =>
        Pass(token => inner.WriteJsonAsync(relativePath, value, token), cancellationToken);

    public Task StartJsonLinesAsync(string relativePath, CancellationToken cancellationToken = default) =>
        Pass(token => inner.StartJsonLinesAsync(relativePath, token), cancellationToken);

    public Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default)
    {
        // Materialised now: the caller's sequence is read when the write lands, which may be later than it lives.
        var lines = values.ToList();
        return Pass(token => inner.AppendJsonLinesAsync(relativePath, lines, token), cancellationToken);
    }

    /// <summary>Lets every write held so far through, in order, and every write from now on as it comes.</summary>
    public Task LetThroughAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _through = true;
            foreach (var write in _held)
            {
                _tail = After(_tail, write, cancellationToken);
            }

            _held.Clear();
            return _tail;
        }
    }

    /// <summary>Held while nothing is let through yet; queued behind every earlier write once it is.</summary>
    private Task Pass(Func<CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_through)
            {
                _held.Add(write);
                return Task.CompletedTask;
            }

            _tail = After(_tail, write, cancellationToken);
            return _tail;
        }
    }

    /// <summary>One write after the one before it, whatever became of that one: a failed write is its own caller's.</summary>
    private static async Task After(Task previous, Func<CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        await Task.WhenAny(previous);
        await write(cancellationToken);
    }
}
