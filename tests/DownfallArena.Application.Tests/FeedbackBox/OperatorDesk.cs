using DownfallArena.Application.Accounts;
using DownfallArena.Application.FeedbackBox;
using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Tests.FeedbackBox;

/// <summary>
/// The operator's use cases on feedback, one class each (#723), wired over one store as the host wires them, so a test
/// reads as the operator's desk: it lists, reads a sheet, marks, plans, files, merges, moves and mutes.
/// </summary>
internal sealed class OperatorDesk(IFeedbackStore store, FeedbackMutes mutes, FeedbackWrites writes, TimeProvider clock)
{
    public Task<FeedbackPage> PageAsync(FeedbackFilter filter, int page, CancellationToken cancellationToken = default) =>
        new ListFeedback(store).HandleAsync(filter, page, cancellationToken);

    public Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default) =>
        new FindFeedback(store).HandleAsync(id, cancellationToken);

    public async Task<FeedbackGroup> GroupAsync(Feedback feedback, CancellationToken cancellationToken = default) =>
        (await new ReadFeedbackSheet(store, mutes, clock).HandleAsync(feedback, cancellationToken)).Group;

    public async Task<FeedbackMute?> MuteOfAsync(Feedback feedback, CancellationToken cancellationToken = default) =>
        (await new ReadFeedbackSheet(store, mutes, clock).HandleAsync(feedback, cancellationToken)).Mute;

    public bool CanMute(Feedback feedback) => mutes.CanMute(feedback);

    public Task<FeedbackWaiting> WaitingAsync(CancellationToken cancellationToken = default) =>
        new CountWaitingFeedback(store, clock).HandleAsync(cancellationToken);

    public Task<Result<FeedbackStatusChange>> SetStatusAsync(string id, FeedbackStatus status, string? reason, string by, CancellationToken cancellationToken = default) =>
        new MarkFeedback(store, mutes, writes, clock).HandleAsync(id, status, reason, by, cancellationToken);

    public Task<Result<FeedbackStatusChange>> PlanAsync(string id, string? plan, CancellationToken cancellationToken = default) =>
        new PlanIdea(store, writes, clock).HandleAsync(id, plan, cancellationToken);

    public Task<Result<FeedbackStatusChange>> FileAsync(string id, int? issue, CancellationToken cancellationToken = default) =>
        new FileBugReport(store, writes, clock).HandleAsync(id, issue, cancellationToken);

    public Task<Result<Feedback>> ReproducedAsync(string id, string scenario, Func<string, Task<bool>> stillKept, CancellationToken cancellationToken = default) =>
        new KeepReproduction(store, writes).HandleAsync(id, scenario, stillKept, cancellationToken);

    public Task<Result<FeedbackMerge>> MergeAsync(string id, string into, CancellationToken cancellationToken = default) =>
        new MergeIdea(store, writes).HandleAsync(id, into, cancellationToken);

    public Task<Result<FeedbackKindChange>> ChangeKindAsync(string id, FeedbackKind kind, CancellationToken cancellationToken = default) =>
        new MoveFeedbackKind(store, writes).HandleAsync(id, kind, cancellationToken);

    public Task<Result<FeedbackMuteChange>> MuteAsync(string id, string by, CancellationToken cancellationToken = default) =>
        new MuteFeedbackSender(store, mutes, clock).HandleAsync(id, by, cancellationToken);

    public Task<Result<FeedbackMuteChange>> UnmuteAsync(string id, CancellationToken cancellationToken = default) =>
        new UnmuteFeedbackSender(store, mutes, clock).HandleAsync(id, cancellationToken);
}
