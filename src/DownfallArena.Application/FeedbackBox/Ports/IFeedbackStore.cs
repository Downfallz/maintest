namespace DownfallArena.Application.FeedbackBox.Ports;

/// <summary>
/// Where feedback is kept (ADR 0189), one record each, beside the recordings in a store of its own. Owned by
/// Application, implemented by Infrastructure (a directory on a laptop, a blob container on Azure).
/// </summary>
public interface IFeedbackStore
{
    /// <summary>Keeps the feedback, in place of what was kept under its id.</summary>
    Task SaveAsync(Feedback feedback, CancellationToken cancellationToken = default);

    /// <summary>Every kept id, the newest first: a listing that reads no record, and that says when each was sent.</summary>
    Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default);

    /// <summary>The feedback by its id, or null when none is kept under it or what is kept cannot be read.</summary>
    Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Lets the feedback go; nothing when none is kept under the id.</summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
