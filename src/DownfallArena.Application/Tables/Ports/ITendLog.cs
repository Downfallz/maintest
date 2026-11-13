using DownfallArena.Application.Tables.Tending;

namespace DownfallArena.Application.Tables.Ports;

/// <summary>
/// Where tending runs are kept (docs/tabletop/admin-plan.md, A3), one record each, beside the recordings: the last
/// <see cref="TendRun.Kept" />, the oldest let go of as new ones come. Owned by Application, implemented by
/// Infrastructure (a directory on a laptop, a blob container on Azure).
/// </summary>
public interface ITendLog
{
    /// <summary>Keeps the run, and lets go of the oldest past <see cref="TendRun.Kept" />.</summary>
    Task RecordAsync(TendRun run, CancellationToken cancellationToken = default);

    /// <summary>Every kept run's id, the newest first: a listing that reads no record.</summary>
    Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default);

    /// <summary>The run by its id, or null when none is kept under it.</summary>
    Task<TendRun?> FindAsync(string id, CancellationToken cancellationToken = default);
}
