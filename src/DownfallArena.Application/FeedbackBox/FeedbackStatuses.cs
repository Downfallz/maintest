using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The status the operator sets on feedback (docs/tabletop/feedback-plan.md, R3, I1, R5), shared by marking, planning
/// and filing: each is idempotent, the status it already has changing nothing. Run under the write gate by its caller.
/// </summary>
internal static class FeedbackStatuses
{
    public const int LongestReason = 300;

    public static async Task<Result<FeedbackStatusChange>> ChangeAsync(IFeedbackStore store, string id, Asked asked, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (status, reason, plan, issue) = asked;
        if (await FeedbackRecords.FindAsync(store, id, cancellationToken) is not { } feedback)
        {
            return Result.Failure<FeedbackStatusChange>(FeedbackErrors.NotFound(id));
        }

        // A merged idea is changed through the one it joined; dismissed, as spam above all, it leaves the group, so a
        // flood merged by mistake neither swells how many asked nor escapes the spam count.
        if (feedback.MergedInto is not null && status != FeedbackStatus.Dismissed)
        {
            return Result.Failure<FeedbackStatusChange>(FeedbackErrors.MergedAway);
        }

        var wanted = Changed(feedback, status, reason, plan, issue, now);
        if (wanted.IsFailure)
        {
            return Result.Failure<FeedbackStatusChange>(wanted.Error);
        }

        var changed = wanted.Value;
        if (changed.Status == feedback.Status && changed.Reason == feedback.Reason && changed.Plan == feedback.Plan && changed.MergedInto == feedback.MergedInto && changed.Issue == feedback.Issue)
        {
            return Result.Success(new FeedbackStatusChange(feedback, Changed: false, Muted: null));
        }

        await store.SaveAsync(changed, cancellationToken);
        if (feedback.MergedInto is { } left && changed.MergedInto is null)
        {
            await FeedbackGroups.LeaveAsync(store, left, feedback.Id, cancellationToken);
        }

        return Result.Success(new FeedbackStatusChange(changed, Changed: true, Muted: null));
    }

    /// <summary>The feedback as the status asked leaves it, or why that status is refused for it.</summary>
    private static Result<Feedback> Changed(Feedback feedback, FeedbackStatus status, string? reason, string? plan, int? issue, DateTimeOffset now)
    {
        var idea = feedback.Kind == FeedbackKind.Idea;
        var oneLine = reason is { Length: > 0 and <= LongestReason } && !reason.Any(char.IsControl);

        // Closed again with the status it has keeps when it was closed: the 180 days run from the first time.
        var closedAt = feedback.Status == status ? feedback.ClosedAt ?? now : now;

        // The plan stays until another replaces it: a misclick on declined, then shipped, still names the plan.
        var reopened = feedback with { Status = status, ClosedAt = null, Reason = null, SpamCounted = false };
        return status switch
        {
            FeedbackStatus.Seen => Result.Success(reopened),
            FeedbackStatus.Reproduced when !idea => Result.Success(reopened),
            FeedbackStatus.Filed when !idea && issue is > 0 => Result.Success(reopened with { Issue = issue }),
            FeedbackStatus.Filed when !idea => Result.Failure<Feedback>(FeedbackErrors.Invalid("Filing names the issue's number, as GitHub gave it.")),

            // Fixed is the issue's: the panel marks it once the issue it was filed as is closed as completed.
            FeedbackStatus.Fixed when !idea && feedback.Issue is not null => Result.Success(reopened with { ClosedAt = closedAt }),
            FeedbackStatus.Fixed when !idea => Result.Failure<Feedback>(FeedbackErrors.NotFiled),
            FeedbackStatus.Planned when idea && PlanPath.Is(plan) => Result.Success(reopened with { Plan = plan }),
            FeedbackStatus.Planned when idea => Result.Failure<Feedback>(FeedbackErrors.Invalid("Planning names the plan's path in the repository: docs/…-plan.md.")),
            FeedbackStatus.Shipped when idea => Result.Success(reopened with { ClosedAt = closedAt }),
            FeedbackStatus.Dismissed when oneLine => Result.Success(reopened with { ClosedAt = closedAt, Reason = reason, MergedInto = null }),
            FeedbackStatus.Declined when idea && oneLine => Result.Success(reopened with { ClosedAt = closedAt, Reason = reason }),
            FeedbackStatus.Dismissed => Result.Failure<Feedback>(FeedbackErrors.Invalid($"Dismissing says why, on one line of {LongestReason} characters at most.")),
            FeedbackStatus.Declined when idea => Result.Failure<Feedback>(FeedbackErrors.Invalid($"Declining says why, on one line of {LongestReason} characters at most.")),
            _ => Result.Failure<Feedback>(FeedbackErrors.Invalid(idea
                ? "An idea is marked seen, planned, shipped, declined or dismissed here."
                : "A bug report is marked seen, reproduced, filed with its issue, fixed or dismissed here.")),
        };
    }

    /// <summary>The status asked for, with what it names: a reason to dismiss or decline, a plan, or an issue's number.</summary>
    public readonly record struct Asked(FeedbackStatus Status, string? Reason = null, string? Plan = null, int? Issue = null);
}
