using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>A bug report turned into an idea, or back (docs/tabletop/feedback-plan.md, R3).</summary>
public sealed class MoveFeedbackKind(IFeedbackStore store, FeedbackWrites writes)
{
    /// <summary>
    /// A bug report the rules answered as designed becomes an idea, or an idea that names a rule gone wrong a bug report;
    /// its words, position and sender kept. An idea others were merged into stays one: a bug report is never grouped.
    /// Whether any were is read from the ideas its own record lists (<see cref="Feedback.Joined" />) that still name it,
    /// under the write gate, so no merge lands between the reading and the change.
    /// </summary>
    public Task<Result<FeedbackKindChange>> HandleAsync(string id, FeedbackKind kind, CancellationToken cancellationToken = default) =>
        writes.OneAtATimeAsync(() => MovingAsync(id, kind, cancellationToken), cancellationToken);

    private async Task<Result<FeedbackKindChange>> MovingAsync(string id, FeedbackKind kind, CancellationToken cancellationToken)
    {
        if (await FeedbackRecords.FindAsync(store, id, cancellationToken) is not { } feedback)
        {
            return Result.Failure<FeedbackKindChange>(FeedbackErrors.NotFound(id));
        }

        if (feedback.MergedInto is not null)
        {
            return Result.Failure<FeedbackKindChange>(FeedbackErrors.MergedAway);
        }

        if (feedback.Kind == kind)
        {
            return Result.Success(new FeedbackKindChange(feedback, Was: null));
        }

        if (feedback.IsClosed)
        {
            return Result.Failure<FeedbackKindChange>(FeedbackErrors.Closed);
        }

        if (kind == FeedbackKind.Bug && (await FeedbackGroups.JoinedAsync(store, feedback, cancellationToken)).Count > 0)
        {
            return Result.Failure<FeedbackKindChange>(FeedbackErrors.Grouped);
        }

        // The two kinds share "the rules" and "something else"; a bug that looks wrong or is stuck is about something else as an idea, and the reverse.
        var categories = kind == FeedbackKind.Bug ? SendFeedback.BugCategories : SendFeedback.IdeaPlaces;
        var changed = feedback with
        {
            Kind = kind,
            Category = categories.Contains(feedback.Category, StringComparer.Ordinal) ? feedback.Category : "other",
            Status = FeedbackStatus.Seen,
        };
        await store.SaveAsync(changed, cancellationToken);
        return Result.Success(new FeedbackKindChange(changed, feedback.Kind));
    }
}
