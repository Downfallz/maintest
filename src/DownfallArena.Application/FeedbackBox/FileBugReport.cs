using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>A bug report marked filed as a public issue (docs/tabletop/feedback-plan.md, R5).</summary>
public sealed class FileBugReport(IFeedbackStore store, FeedbackWrites writes, TimeProvider clock)
{
    /// <summary>
    /// Marks a bug report filed as the public issue the operator filed it as, which reopens it when it was closed.
    /// Filed again under the same number, nothing changes; another number replaces a mistyped one.
    /// </summary>
    public Task<Result<FeedbackStatusChange>> HandleAsync(string id, int? issue, CancellationToken cancellationToken = default) =>
        writes.OneAtATimeAsync(
            () => FeedbackStatuses.ChangeAsync(store, id, FeedbackStatus.Filed, null, plan: null, issue, clock.GetUtcNow(), cancellationToken),
            cancellationToken);
}
