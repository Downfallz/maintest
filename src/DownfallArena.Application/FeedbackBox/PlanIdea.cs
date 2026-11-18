using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>An idea marked planned, with the plan it went into (docs/tabletop/feedback-plan.md, I1).</summary>
public sealed class PlanIdea(IFeedbackStore store, FeedbackWrites writes, TimeProvider clock)
{
    /// <summary>Marks an idea planned, with the path of the plan it went into (<see cref="PlanPath" />); that reopens it when it was closed.</summary>
    public Task<Result<FeedbackStatusChange>> HandleAsync(string id, string? plan, CancellationToken cancellationToken = default) =>
        writes.OneAtATimeAsync(
            () => FeedbackStatuses.ChangeAsync(store, id, FeedbackStatus.Planned, null, plan?.Trim(), issue: null, clock.GetUtcNow(), cancellationToken),
            cancellationToken);
}
