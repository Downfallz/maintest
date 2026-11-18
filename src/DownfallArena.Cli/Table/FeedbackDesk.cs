using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;

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
    MoveFeedbackKind Move,
    MuteFeedbackSender Mute,
    UnmuteFeedbackSender Unmute)
{
    public static FeedbackDesk Open(IFeedbackStore store, FeedbackMutes mutes, FeedbackWrites writes, TimeProvider clock) => new(
        new ListFeedback(store),
        new FindFeedback(store),
        new ReadFeedbackSheet(store, mutes, clock),
        new MarkFeedback(store, mutes, writes, clock),
        new PlanIdea(store, writes, clock),
        new FileBugReport(store, writes, clock),
        new KeepReproduction(store, writes),
        new MergeIdea(store, writes),
        new MoveFeedbackKind(store, writes),
        new MuteFeedbackSender(store, mutes, clock),
        new UnmuteFeedbackSender(store, mutes, clock));
}
