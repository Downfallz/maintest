using DownfallArena.Application.Accounts;
using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>What the operator's sheet reads beside the feedback itself (docs/tabletop/feedback-plan.md, R3, I1).</summary>
public sealed class ReadFeedbackSheet(IFeedbackStore store, FeedbackMutes mutes, TimeProvider clock)
{
    /// <summary>
    /// An idea's group as its sheet reads it: the ideas merged into it and how many asked, or the idea it was merged into,
    /// read from the ideas the group's idea lists, never the whole store; a bug report is never grouped. And the mute that
    /// holds on its sender now, if any, and whether one can be kept for it.
    /// </summary>
    public async Task<FeedbackSheet> HandleAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        var mute = await mutes.OfSenderAsync(feedback, clock.GetUtcNow(), cancellationToken);
        return new FeedbackSheet(await GroupAsync(feedback, cancellationToken), mute, mutes.CanMute(feedback));
    }

    private async Task<FeedbackGroup> GroupAsync(Feedback feedback, CancellationToken cancellationToken)
    {
        if (feedback.Kind == FeedbackKind.Bug)
        {
            return new FeedbackGroup(null, [], FeedbackGroups.AsksOf([feedback]));
        }

        if (feedback.MergedInto is not null)
        {
            return new FeedbackGroup(await FeedbackGroups.RootAsync(store, feedback, cancellationToken), [], FeedbackGroups.AsksOf([feedback]));
        }

        // Read again: its list is what changed since the caller read it, when a merge just landed.
        var current = await FeedbackRecords.FindAsync(store, feedback.Id, cancellationToken) ?? feedback;
        var joined = await FeedbackGroups.JoinedAsync(store, current, cancellationToken);
        return new FeedbackGroup(null, joined, FeedbackGroups.AsksOf([current, .. joined]));
    }
}
