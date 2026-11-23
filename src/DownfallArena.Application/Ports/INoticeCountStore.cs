using DownfallArena.Application.Work;

namespace DownfallArena.Application.Ports;

/// <summary>
/// Where the notices worker's counts are kept (notifications plan, N6): one document a UTC day, read with its version
/// and written only over the version read, since the API and the worker app both count. The rules -- what adds to
/// what, how many days are read -- are <see cref="NoticeCounts" />'s. Owned by Application, implemented by
/// Infrastructure (a directory on a laptop, a blob container on Azure).
/// </summary>
public interface INoticeCountStore
{
    /// <summary>The day's counts as stored, and their version; none and no version for a day nothing was counted on.</summary>
    Task<NoticeCountRead> ReadAsync(DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the day's counts only if they are still at <paramref name="version" /> (none: only if the day has no
    /// document yet). False when another write came first.
    /// </summary>
    Task<bool> WriteAsync(DateOnly day, IReadOnlyList<NoticeCount> counts, string? version, CancellationToken cancellationToken = default);

    /// <summary>Lets go of every day before <paramref name="day" />: what nobody reads any more.</summary>
    Task ForgetBeforeAsync(DateOnly day, CancellationToken cancellationToken = default);
}
