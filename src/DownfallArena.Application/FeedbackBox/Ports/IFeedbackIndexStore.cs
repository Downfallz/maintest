namespace DownfallArena.Application.FeedbackBox.Ports;

/// <summary>
/// Where the index of feedback is kept (#866), beside the records it summarises. Owned by Application, implemented by
/// Infrastructure. More than one process writes it (the API, the tending job, two revisions overlapping), so a write
/// names the version it read and is refused when another came first.
/// </summary>
public interface IFeedbackIndexStore
{
    /// <summary>The index, and its version; none when it was never written or cannot be read.</summary>
    Task<FeedbackIndexRead> ReadIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the index in place of the version read (<see cref="FeedbackIndexRead.Version" />), and answers false,
    /// writing nothing, when another write came since.
    /// </summary>
    Task<bool> WriteIndexAsync(FeedbackIndexContent content, string? version, CancellationToken cancellationToken = default);
}
