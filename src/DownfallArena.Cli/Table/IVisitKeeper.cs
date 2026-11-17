namespace DownfallArena.Cli.Table;

/// <summary>Where a recorded table keeps each seat's last visit (ADR 0194): read with the tag of what was read, written only over it.</summary>
internal interface IVisitKeeper
{
    /// <summary>The seat's last visit, and the tag of what was read; none and no tag before the first one.</summary>
    Task<(LastVisit? Visit, string? Tag)> VisitAsync(string seat, CancellationToken cancellationToken = default);

    /// <summary>Writes the seat's last visit if what is kept is still what was read under <paramref name="expected" />; the new tag, or none when it moved.</summary>
    Task<string?> WriteVisitIfAsync(string seat, LastVisit visit, string? expected, CancellationToken cancellationToken = default);
}
