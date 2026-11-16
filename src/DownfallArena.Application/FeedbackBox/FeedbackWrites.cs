namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// One write to feedback at a time in this process (ADR 0189): the guards read the day's feedback before they save, and
/// a burst sent at once would otherwise each read the same history and pass them all; an account forgotten while a
/// report of its is merged would get its id written back. The API runs one process, so a gate in it is enough.
/// </summary>
public sealed class FeedbackWrites : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Runs the write once every write before it has finished.</summary>
    public async Task<T> OneAtATimeAsync<T>(Func<Task<T>> write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await write();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
