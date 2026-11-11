using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One table as the host holds it: its session, the API over it, its seats and their codes, its pilot, and
/// the recording it writes. The host is a registry of these (ADR 0081), and everything a request can reach
/// is reached through one of them.
/// </summary>
internal sealed class PlayedTable : IDisposable
{
    private readonly CancellationTokenSource _stopping;
    private long _touched;
    private int _disposed;
    private int _closed;

    public PlayedTable(
        TableOpening opening,
        ITableSession session,
        TableApi api,
        IReadOnlyList<TableSeat> seats,
        TablePilot pilot,
        PlaytestRun? run,
        CancellationTokenSource stopping)
    {
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentException.ThrowIfNullOrWhiteSpace(opening.Id);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(seats);
        ArgumentNullException.ThrowIfNull(pilot);
        ArgumentNullException.ThrowIfNull(stopping);

        Id = opening.Id;
        Session = session;
        Api = api;
        Seats = seats;
        Pilot = pilot;
        Run = run;
        Request = opening.Request;
        _stopping = stopping;
        CreatedAt = opening.At;
        _touched = opening.At.UtcTicks;
    }

    /// <summary>The session id: the run's name in its store, and what a page, a code and a pilot token all name.</summary>
    public string Id { get; }

    public int EventStreamVersion { get; init; } = MatchTrace.CurrentEventStreamVersion;

    /// <summary>The hash of the content the table is played on (ADR 0163), which its record keeps; none for a table built without one.</summary>
    public string? Content { get; init; }

    public ITableSession Session { get; }

    public TableApi Api { get; }

    public IReadOnlyList<TableSeat> Seats { get; }

    public TablePilot Pilot { get; }

    /// <summary>Seats explicitly offered in the player lobby; claims are serialized and recorded by the lobby.</summary>
    public IReadOnlyList<string> OpenSeats { get; set; } = [];

    /// <summary>Opened as unranked (ADR 0155): its result is kept and listed, never rated on the ladder.</summary>
    public bool Casual { get; set; }

    /// <summary>The recording, or none for a table told to keep nothing.</summary>
    public PlaytestRun? Run { get; }

    /// <summary>What the table was asked to be, seed included, which is what its record writes down (ADR 0091).</summary>
    public TableRequest? Request { get; }

    /// <summary>
    /// Writes the table down for a host that comes after this one (ADR 0091): the request, the match id, and
    /// the tokens and codes its players hold, and the index of the tables after it (ADR 0161). A table that
    /// records nothing is still kept in the index, which is how its host finds and counts it.
    /// </summary>
    public async Task RecordAsync(CancellationToken cancellationToken = default)
    {
        if (Request is not { } request)
        {
            return;
        }

        await (Run is { } run ? run.RecordAsync(TableRecord.Of(this, request), cancellationToken) : IndexedAsync(TableRecord.Of(this, request)));
        Recorded?.Invoke();
    }

    /// <summary>
    /// Raised once a write of the record has landed: what another process reads to see a seat reached, given or kept,
    /// so what tells it to look (ADR 0165) is said after the write, never before it, when the look would find nothing.
    /// </summary>
    public event Action? Recorded;

    /// <summary>
    /// Marks how the table ended -- finished, or closed without finishing -- in its record, or, for a table that
    /// records nothing, in the index alone. A write that fails is a line on the console.
    /// </summary>
    public async Task MarkAsync(string status, CancellationToken cancellationToken = default)
    {
        await MarkedAsync(status, cancellationToken);
        Recorded?.Invoke();
    }

    private Task MarkedAsync(string status, CancellationToken cancellationToken)
    {
        if (Run is { } run)
        {
            return run.MarkAsync(status, cancellationToken);
        }

        TableRecord? indexed;
        lock (_indexGate)
        {
            indexed = _indexed;

            // Nothing indexed yet, which a table of two bots can end before: the first record says it.
            _markedFirst ??= indexed is null ? status : null;
        }

        return indexed is not null && indexed.Status != status ? IndexedAsync(indexed with { Status = status }) : Task.CompletedTask;
    }

    /// <summary>
    /// The index a table that records nothing keeps itself in (ADR 0161), its record being held here and never
    /// written; a recorded table is indexed by its run, after each record write.
    /// </summary>
    public Application.Tables.Ports.ITableIndex? Index
    {
        get => _indexer?.Index;
        init => _indexer = value is null ? null : new TableIndexer(value, Id);
    }

    private readonly TableIndexer? _indexer;
    private readonly Lock _indexGate = new();
    private TableRecord? _indexed;
    private string? _markedFirst;
    private Task _indexing = Task.CompletedTask;

    /// <summary>The record a table that records nothing would have written, into the index, after the one before it.</summary>
    private Task IndexedAsync(TableRecord record)
    {
        if (Run is not null || _indexer is not { } indexer)
        {
            return Task.CompletedTask;
        }

        lock (_indexGate)
        {
            var before = _indexed;
            var written = (before is null && _markedFirst is { } ended ? record with { Status = ended } : record).Over(before);
            _indexed = written;
            // Never cancelled by the table's own stopping: an index write is what lets the lobby see how it ended.
            return _indexing = AfterAsync(_indexing, () => indexer.FollowAsync(before, written, CancellationToken.None));
        }

        static async Task AfterAsync(Task before, Func<Task> next)
        {
            await Task.WhenAny(before);
            await next();
        }
    }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>The last moment a request reached this table, which is what says whether anybody is still at it.</summary>
    public DateTimeOffset Touched => new(Interlocked.Read(ref _touched), TimeSpan.Zero);

    public bool IsOver => Session.IsOver;

    /// <summary>
    /// Whether the table is finished with its files too: over, and the recording closed, or given up on, or
    /// never opened. A table that is over but still writing must not be dropped from under its own last
    /// note; one whose match failed, or whose files could not be written, is finished all the same, or it
    /// would be neither under way nor sweepable and stay for the life of the host.
    /// </summary>
    public bool IsFinished => IsOver && (Run is null || Volatile.Read(ref _closed) != 0);

    /// <summary>Says the recording is done with, written or not: the closing has run to its end.</summary>
    public void MarkClosed() => Volatile.Write(ref _closed, 1);

    /// <summary>The record being marked closed, once the table is closed unfinished; complete otherwise.</summary>
    public Task Closing { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Closes the table for good: the operator closed it, or it never began and nobody came to it (ADR 0115). Its record is marked
    /// closed, so a host that comes after this one does not rebuild it, and then it is let go of. The one
    /// way a record is marked closed: a host stopping lets its tables go without closing them, since the
    /// next host rebuilding them is the whole point (ADR 0091).
    /// </summary>
    public void Close()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        // Read before the cancellation, which ends the session at once: a table let go of inside Cancel
        // itself reads as over by the time it is asked whether it was.
        if (!IsOver)
        {
            Closing = MarkAsync(TableRecord.Closed, CancellationToken.None);
        }

        Dispose();
    }

    public void Touch(DateTimeOffset now) => Interlocked.Exchange(ref _touched, now.UtcTicks);

    /// <summary>
    /// After the table is let go of: waits for its session to have stopped -- whatever step it was taking
    /// finished -- and for every write of its recording to land, so what is done to its run next is done after
    /// the match and not beside it. Bounded: a step that is not coming back must not hold the operator's request.
    /// </summary>
    public async Task QuiesceAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAny(Session.Outcome, Task.Delay(QuiesceWithin, cancellationToken));

        // The closed marker is one of the writes to wait for: landing after a deletion, it would be the one
        // file of a run that was deleted, and the run would be listed again.
        await Task.WhenAny(Closing, Task.Delay(QuiesceWithin, cancellationToken));
        if (Run is { } run)
        {
            await run.DrainAsync(cancellationToken);
        }
    }

    private static readonly TimeSpan QuiesceWithin = TimeSpan.FromSeconds(5);

    /// <summary>The round the match has reached, off seat 1's board, or none before the first.</summary>
    public async Task<int?> RoundAsync()
    {
        // Never cancelled by the table's own stopping: a listing reads a table that is being let go of, and
        // a disposed source is an exception where a null would do.
        var board = await Session.BoardAsync(PlayerSlot.Player1);
        return board.IsSuccess ? board.Value.RoundNumber : null;
    }

    /// <summary>
    /// Stops the match if it is still being played and lets the session go, which ends it without an outcome;
    /// the recording keeps what it has, which is what an abandoned session is, and its record keeps saying open: letting a table go is
    /// what a host does as it stops, and the next host rebuilds what it let go of. Closing a table for good
    /// is <see cref="Close" />. Once only: the registry lets a table go and a test holds it too, and neither
    /// should have to know about the other.
    /// </summary>
    /// <remarks>
    /// The session is let go of once its match has stopped, not now: a step may be inside a command, and the
    /// session lets its match go only once that step is over. Cancelling is what stops it; the outcome
    /// completing is what says it has.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stopping.Cancel();
        Api.Detach();
        Session.Outcome.ContinueWith(
            _ =>
            {
                Session.Dispose();
                _stopping.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
