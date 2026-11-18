using DownfallArena.Application.FeedbackBox;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The operator's use cases on feedback that the Feedback tab calls (docs/tabletop/feedback-plan.md, ADR 0189), one
/// class each (#723), wired over one store, one mute book and one write gate: the composition root's, not a use case.
/// </summary>
internal sealed record FeedbackDesk(
    ListFeedback List,
    FindFeedback Find,
    ReadFeedbackSheet Sheet,
    MarkFeedback Mark,
    PlanIdea Plan,
    FileBugReport File,
    KeepReproduction Reproduced,
    MergeIdea Merge,
    UnmergeIdea Unmerge,
    MoveFeedbackKind Move,
    MuteFeedbackSender Mute,
    UnmuteFeedbackSender Unmute)
{
    /// <summary>The desk over a store's index (#866): every use case writes through <see cref="FeedbackIndex.Records" />.</summary>
    public static FeedbackDesk Open(FeedbackIndex index, FeedbackMutes mutes, FeedbackWrites writes, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(index);
        var store = index.Records;
        return new(
            new ListFeedback(index),
            new FindFeedback(store),
            new ReadFeedbackSheet(store, mutes, clock),
            new MarkFeedback(index, mutes, writes, clock),
            new PlanIdea(store, writes, clock),
            new FileBugReport(store, writes, clock),
            new KeepReproduction(store, writes),
            new MergeIdea(store, writes),
            new UnmergeIdea(store, writes),
            new MoveFeedbackKind(store, writes),
            new MuteFeedbackSender(store, mutes, clock),
            new UnmuteFeedbackSender(store, mutes, clock));
    }
}
