namespace DownfallArena.Cli.Table;

/// <summary>
/// A seat's last visit (ADR 0194): the last feed sequence the seat was shown of its game, and when the host was told
/// it was shown it.
/// </summary>
internal sealed record LastVisit(long Sequence, DateTimeOffset At);
