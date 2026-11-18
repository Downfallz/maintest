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

    /// <summary>
    /// The ids of feedback sent after <paramref name="after" /> and no later than <paramref name="until" />, the newest
    /// first. Ids start with the day they were sent, so a store lists those days alone and never every record kept:
    /// what a player's sending reads on every request, refused ones included (#693). A caller that reads up to now
    /// passes a little after it (<see cref="SendFeedback.Ahead" />), so an id stamped by a clock a moment ahead is read.
    /// </summary>
    Task<IReadOnlyList<string>> IdsSentAsync(DateTimeOffset after, DateTimeOffset until, CancellationToken cancellationToken = default);

    /// <summary>The feedback by its id, or null when none is kept under it or what is kept cannot be read.</summary>
    Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Lets the feedback go; nothing when none is kept under the id.</summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
