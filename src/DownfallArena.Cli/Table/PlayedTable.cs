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
        TableSession session,
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
        _stopping = stopping;
        CreatedAt = opening.At;
        _touched = opening.At.UtcTicks;
    }

    /// <summary>The session id: the run's name in its store, and what a page, a code and a pilot token all name.</summary>
    public string Id { get; }

    public TableSession Session { get; }

    public TableApi Api { get; }

    public IReadOnlyList<TableSeat> Seats { get; }

    public TablePilot Pilot { get; }

    /// <summary>The recording, or none for a table told to keep nothing.</summary>
    public PlaytestRun? Run { get; }

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

    public void Touch(DateTimeOffset now) => Interlocked.Exchange(ref _touched, now.UtcTicks);

    /// <summary>
    /// After the table is let go of: waits for its driver to stop and for every write of its recording to
    /// land, so what is done to its run next is done after the match and not beside it. Bounded: a driver
    /// that is not coming back must not hold the operator's request.
    /// </summary>
    public async Task QuiesceAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAny(Session.Outcome, Task.Delay(QuiesceWithin, cancellationToken));
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
        var board = await Session.Queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(Session.MatchId, PlayerSlot.Player1), CancellationToken.None);
        return board.IsSuccess ? board.Value.RoundNumber : null;
    }

    /// <summary>
    /// Stops the match if it is still being played and lets the session go. A seat waiting on a person is
    /// released with a cancellation, which ends the driver without an outcome; the recording keeps what it
    /// has, which is what an abandoned session is. Once only: the registry lets a table go and a test holds
    /// it too, and neither should have to know about the other.
    /// </summary>
    /// <remarks>
    /// The session is let go of once the driver has stopped, not now: the driver may be inside a command,
    /// holding the gate the session disposes, and a gate disposed under it is an exception where an outcome
    /// should be. Cancelling is what ends the driver; the outcome completing is what says it has.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stopping.Cancel();
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
