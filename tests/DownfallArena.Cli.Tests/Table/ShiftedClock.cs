namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The system's clock moved on by a test (#1180): the time it says is the system's plus what the test added, and its
/// timers run in real time. A host's waits (a closing table looked at every 100 ms) still end, which a clock stopped
/// in time never lets them do.
/// </summary>
internal sealed class ShiftedClock : TimeProvider
{
    private long _aheadTicks;

    public void Advance(TimeSpan by) => Interlocked.Add(ref _aheadTicks, by.Ticks);

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref _aheadTicks));
}
