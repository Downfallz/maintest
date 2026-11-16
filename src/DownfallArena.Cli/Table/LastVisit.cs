namespace DownfallArena.Cli.Table;

/// <summary>
/// A seat's last visit (ADR 0194): the last feed sequence the seat was shown of its game, when the host was told, and
/// a stamp of the seat key it was told under (<see cref="Holder" />).
/// </summary>
/// <param name="Holder">
/// A stamp of the seat's key, never the key: a seat offered again gets a fresh key (ADR 0170), and the mark of the
/// player who left is then nobody's, neither served to the next player nor kept against their own.
/// </param>
internal sealed record LastVisit(long Sequence, DateTimeOffset At, string Holder);
