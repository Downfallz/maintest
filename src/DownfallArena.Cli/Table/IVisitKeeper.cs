namespace DownfallArena.Cli.Table;

/// <summary>Where a recorded table keeps each seat's last visit (ADR 0194): read with the tag of what was read, written only over it.</summary>
internal interface IVisitKeeper
{
    /// <summary>The seat's last visit, and the tag of what was read; none and no tag before the first one.</summary>
    Task<(LastVisit? Visit, string? Tag)> VisitAsync(string seat, CancellationToken cancellationToken = default);

    /// <summary>Writes the seat's last visit if what is kept is still what was read under <paramref name="expected" />; the new tag, or none when it moved.</summary>
    Task<string?> WriteVisitIfAsync(string seat, LastVisit visit, string? expected, CancellationToken cancellationToken = default);

    /// <summary>Whether the table's record is still kept: not once the operator deleted its run, which a first write would bring back (#789).</summary>
    Task<bool> RecordedAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes the seat's mark only if it is still the one written under <paramref name="expected" />. Undoes a first write made while the run was being deleted (#818).</summary>
    Task<bool> ForgetVisitIfAsync(string seat, string expected, CancellationToken cancellationToken = default);
}
