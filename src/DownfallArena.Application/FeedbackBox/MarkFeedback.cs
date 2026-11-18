using DownfallArena.Application.Accounts;
using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The status the operator sets by hand on feedback (docs/tabletop/feedback-plan.md, R3, I1, ADR 0189). Three pieces
/// dismissed as spam within 30 days mute their sender on their own. Every write goes through
/// <see cref="FeedbackWrites" />, so a status set while the sender adds to the same feedback loses neither; counting spam
/// marks reads the index's rows (#866), outside it, so the players' sending never waits on it.
/// </summary>
public sealed class MarkFeedback(FeedbackIndex index, FeedbackMutes mutes, FeedbackWrites writes, TimeProvider clock)
{
    private readonly IFeedbackStore _store = index.Records;

    /// <summary>
    /// Marks it seen, which reopens it when it was closed; reproduced, for a bug report; shipped or declined, for an idea,
    /// declining with a reason kept for the players who asked; or dismissed, with a reason of one line,
    /// <see cref="Feedback.SpamReason" /> marking it as spam. Planned is <see cref="PlanIdea" />'s, since it names a plan,
    /// and filed <see cref="FileBugReport" />'s, since it names an issue. The status it already has, with the same
    /// reason, changes nothing. An idea merged into another takes none but dismissed, which takes it out of its group.
    /// A plan stays until another replaces it.
    /// </summary>
    public async Task<Result<FeedbackStatusChange>> HandleAsync(string id, FeedbackStatus status, string? reason, string by, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        var set = await writes.OneAtATimeAsync(
            () => FeedbackStatuses.ChangeAsync(_store, id, new FeedbackStatuses.Asked(status, Reason: reason?.Trim()), clock.GetUtcNow(), cancellationToken),
            cancellationToken);
        if (set.IsFailure || !set.Value.Feedback.IsSpam || set.Value.Feedback.SpamCounted)
        {
            return set;
        }

        // Counted outside the gate, then marked counted: a failure in between leaves it to count when asked again.
        var muted = await MutedForSpamAsync(set.Value.Feedback, by, cancellationToken);
        var counted = await writes.OneAtATimeAsync(() => CountedAsync(id, cancellationToken), cancellationToken);
        return Result.Success(set.Value with { Feedback = counted ?? set.Value.Feedback, Muted = muted });
    }

    /// <summary>Marks a spam mark counted, as it is now; nothing when it is no longer one.</summary>
    private async Task<Feedback?> CountedAsync(string id, CancellationToken cancellationToken)
    {
        if (await FeedbackRecords.FindAsync(_store, id, cancellationToken) is not { IsSpam: true, SpamCounted: false } marked)
        {
            return null;
        }

        var counted = marked with { SpamCounted = true };
        await _store.SaveAsync(counted, cancellationToken);
        return counted;
    }

    /// <summary>The mute a third spam mark within 30 days earns its sender, or none when it is not the third or a mute holds already.</summary>
    private async Task<FeedbackMute?> MutedForSpamAsync(Feedback marked, string by, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (!mutes.CanMute(marked) || await mutes.FindAsync(marked.Sender, marked.Account, now, cancellationToken) is not null)
        {
            return null;
        }

        // This mark is counted from its record, the others from the index's rows: a mark the index missed is this one.
        var marks = 1 + (await index.RowsAsync(cancellationToken))
            .Count(one => one.Id != marked.Id && one.Sender == marked.Sender && one.Spam && one.ClosedAt is { } closed && now - closed <= FeedbackMute.SpamWindow);
        if (marks < FeedbackMute.SpamMarks)
        {
            return null;
        }

        var mute = FeedbackMute.Begun(now, by, FeedbackMute.BySpam);
        await mutes.SetAsync(marked.Sender, marked.Account, mute, cancellationToken);
        return mute;
    }
}
