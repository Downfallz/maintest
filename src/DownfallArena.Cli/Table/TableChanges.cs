namespace DownfallArena.Cli.Table;

/// <summary>
/// That something about a table changed, for the pages waiting to hear it (ADR 0119): a question asked or
/// answered, a seat reached or left, the match begun or over. It says nothing of what changed -- the seat
/// route says that, one seat at a time -- only that a page should ask again now rather than on its next poll.
/// </summary>
internal sealed class TableChanges
{
    private readonly Lock _gate = new();
    private TaskCompletionSource _next = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _version;
    private bool _closed;

    /// <summary>How many times the table changed, which is what a waiter names to be woken for the next one.</summary>
    public long Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    /// <summary>Whether the table is gone: every waiter was let go, and nothing will change again.</summary>
    public bool IsClosed
    {
        get
        {
            lock (_gate)
            {
                return _closed;
            }
        }
    }

    public void Bump()
    {
        TaskCompletionSource waiting;
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _version++;
            waiting = _next;
            _next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        waiting.TrySetResult();
    }

    /// <summary>The table is let go of: whoever waits is woken, and told nothing more is coming.</summary>
    public void Close()
    {
        TaskCompletionSource waiting;
        lock (_gate)
        {
            _closed = true;
            waiting = _next;
        }

        waiting.TrySetResult();
    }

    /// <summary>
    /// The version once it has moved past <paramref name="seen" />, or the version as it stands once the table is
    /// closed. A waiter that saw nothing yet names 0 and is answered by the first change.
    /// </summary>
    public async Task<long> WaitAsync(long seen, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task next;
            lock (_gate)
            {
                if (_closed || _version > seen)
                {
                    return _version;
                }

                next = _next.Task;
            }

            await next.WaitAsync(cancellationToken);
        }
    }
}
