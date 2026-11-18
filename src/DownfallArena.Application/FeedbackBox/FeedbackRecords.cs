using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>How the operator's use cases read feedback from its store (ADR 0189).</summary>
internal static class FeedbackRecords
{
    /// <summary>The feedback by its id, or none for an id no feedback could have.</summary>
    public static Task<Feedback?> FindAsync(IFeedbackStore store, string id, CancellationToken cancellationToken) =>
        Feedback.IsId(id) ? store.FindAsync(id, cancellationToken) : Task.FromResult<Feedback?>(null);

    /// <summary>The feedback, read once, when its sender can be muted; or why not.</summary>
    public static async Task<Result<Feedback>> MuteableAsync(IFeedbackStore store, FeedbackMutes mutes, string id, CancellationToken cancellationToken) =>
        await FindAsync(store, id, cancellationToken) switch
        {
            null => Result.Failure<Feedback>(FeedbackErrors.NotFound(id)),
            { } found when !mutes.CanMute(found) => Result.Failure<Feedback>(FeedbackErrors.NoSender),
            { } found => Result.Success(found),
        };

    /// <summary>
    /// Every record kept, the newest first. Only the operator's list, the Overview's card and the spam count read it,
    /// never a player's sending (#693), and never under the write gate, so the players' sending never waits on it.
    /// </summary>
    public static async Task<List<Feedback>> AllAsync(IFeedbackStore store, CancellationToken cancellationToken)
    {
        var all = new List<Feedback>();
        foreach (var id in await store.IdsAsync(cancellationToken))
        {
            if (await store.FindAsync(id, cancellationToken) is { } found)
            {
                all.Add(found);
            }
        }

        return all;
    }
}
