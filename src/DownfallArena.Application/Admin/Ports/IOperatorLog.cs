namespace DownfallArena.Application.Admin.Ports;

/// <summary>
/// Where the operators' actions are kept (admin plan, A6), one record each, beside the recordings: the last
/// <see cref="OperatorAction.Kept" />, the oldest let go of as new ones come. Owned by Application, implemented by
/// Infrastructure (a directory on a laptop, a blob container on Azure).
/// </summary>
public interface IOperatorLog
{
    /// <summary>Keeps the action, and lets go of the oldest past <see cref="OperatorAction.Kept" />.</summary>
    Task RecordAsync(OperatorAction action, CancellationToken cancellationToken = default);

    /// <summary>Every kept action's id, the newest first: a listing that reads no record.</summary>
    Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default);

    /// <summary>The action by its id, or null when none is kept under it.</summary>
    Task<OperatorAction?> FindAsync(string id, CancellationToken cancellationToken = default);
}
