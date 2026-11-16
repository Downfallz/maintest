using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The operator's side of feedback (docs/tabletop/feedback-plan.md, R3, ADR 0189): the Feedback tab's list, a sheet,
/// the status set by hand, a bug report turned into an idea and back, and the sender muted or let speak again. Three
/// pieces dismissed as spam within 30 days mute their sender on their own. Every write goes through
/// <see cref="FeedbackWrites" />, so a status set while the sender adds to the same feedback loses neither.
/// </summary>
public sealed class OperatorFeedback(IFeedbackStore store, FeedbackMutes mutes, FeedbackWrites writes, TimeProvider clock)
{
    public const int PerPage = 20;

    public const int LongestReason = 300;

    /// <summary>A page of what the filter keeps, newest first; a page past the last is the last.</summary>
    public async Task<FeedbackPage> PageAsync(FeedbackFilter filter, int page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var kept = (await AllAsync(cancellationToken)).Where(filter.Keeps).ToList();
        var pages = Math.Max(1, (kept.Count + PerPage - 1) / PerPage);
        var shown = Math.Clamp(page, 1, pages);
        return new FeedbackPage([.. kept.Skip((shown - 1) * PerPage).Take(PerPage)], kept.Count, shown, pages);
    }

    public Task<Feedback?> FindAsync(string id, CancellationToken cancellationToken = default) =>
        Feedback.IsId(id) ? store.FindAsync(id, cancellationToken) : Task.FromResult<Feedback?>(null);

    /// <summary>The mute that holds on the feedback's sender now, or none.</summary>
    public Task<FeedbackMute?> MuteOfAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        return feedback.Sender == ForgetFeedbackAccount.Gone
            ? Task.FromResult<FeedbackMute?>(null)
            : mutes.FindAsync(feedback.Sender, feedback.Account, clock.GetUtcNow(), cancellationToken);
    }

    /// <summary>
    /// Marks it seen, which reopens it when it was dismissed; reproduced, for a bug report; or dismissed, with a reason,
    /// <see cref="Feedback.SpamReason" /> marking it as spam.
    /// </summary>
    public Task<Result<FeedbackStatusChange>> SetStatusAsync(string id, FeedbackStatus status, string? reason, string by, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        return writes.OneAtATimeAsync(() => StatusAsync(id, status, reason?.Trim(), by, cancellationToken), cancellationToken);
    }

    /// <summary>A bug report the rules answered as designed becomes an idea, or an idea that names a rule gone wrong a bug report; its words, position and sender kept.</summary>
    public Task<Result<Feedback>> ChangeKindAsync(string id, FeedbackKind kind, CancellationToken cancellationToken = default) =>
        writes.OneAtATimeAsync(() => KindAsync(id, kind, cancellationToken), cancellationToken);

    /// <summary>Stops the feedback's sender from sending more for <see cref="FeedbackMute.Lasts" />.</summary>
    public async Task<Result<FeedbackMute>> MuteAsync(string id, string by, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        if (await SenderOfAsync(id, cancellationToken) is { IsFailure: true } refused)
        {
            return Result.Failure<FeedbackMute>(refused.Error);
        }

        var feedback = (await FindAsync(id, cancellationToken))!;
        var mute = FeedbackMute.Begun(clock.GetUtcNow(), by, FeedbackMute.ByOperator);
        return await mutes.SetAsync(feedback.Sender, feedback.Account, mute, cancellationToken)
            ? Result.Success(mute)
            : Result.Failure<FeedbackMute>(FeedbackErrors.NoSender);
    }

    /// <summary>Lets the feedback's sender send again.</summary>
    public async Task<Result> UnmuteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (await SenderOfAsync(id, cancellationToken) is { IsFailure: true } refused)
        {
            return refused;
        }

        var feedback = (await FindAsync(id, cancellationToken))!;
        return await mutes.SetAsync(feedback.Sender, feedback.Account, null, cancellationToken) ? Result.Success() : Result.Failure(FeedbackErrors.NoSender);
    }

    /// <summary>What the Overview's card counts.</summary>
    public async Task<FeedbackWaiting> WaitingAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var lastDay = (await store.IdsAsync(cancellationToken)).Count(id => Feedback.SentAt(id) is { } at && at > now - SendFeedback.Day);
        var waiting = (await AllAsync(cancellationToken)).Where(one => one.Status == FeedbackStatus.New).ToList();
        return new FeedbackWaiting(waiting.Count, waiting.Count(one => one.Kind == FeedbackKind.Bug), lastDay, lastDay >= SendFeedback.PerHost);
    }

    private async Task<Result<FeedbackStatusChange>> StatusAsync(string id, FeedbackStatus status, string? reason, string by, CancellationToken cancellationToken)
    {
        if (await FindAsync(id, cancellationToken) is not { } feedback)
        {
            return Result.Failure<FeedbackStatusChange>(FeedbackErrors.NotFound(id));
        }

        var now = clock.GetUtcNow();
        Feedback changed;
        switch (status)
        {
            case FeedbackStatus.Seen:
                changed = feedback with { Status = status, ClosedAt = null, Reason = null };
                break;
            case FeedbackStatus.Reproduced when feedback.Kind == FeedbackKind.Bug:
                changed = feedback with { Status = status, ClosedAt = null, Reason = null };
                break;
            case FeedbackStatus.Dismissed when reason is { Length: > 0 and <= LongestReason }:
                changed = feedback with { Status = status, ClosedAt = now, Reason = reason };
                break;
            case FeedbackStatus.Dismissed:
                return Result.Failure<FeedbackStatusChange>(FeedbackErrors.Invalid($"Dismissing says why, in {LongestReason} characters at most."));
            default:
                return Result.Failure<FeedbackStatusChange>(FeedbackErrors.Invalid(feedback.Kind == FeedbackKind.Bug
                    ? "A bug report is marked seen, reproduced or dismissed here."
                    : "An idea is marked seen or dismissed here."));
        }

        await store.SaveAsync(changed, cancellationToken);
        return Result.Success(new FeedbackStatusChange(changed, changed.IsSpam ? await MutedForSpamAsync(changed, by, now, cancellationToken) : null));
    }

    /// <summary>The mute a third spam mark within 30 days earns its sender, or none when it is not the third or a mute holds already.</summary>
    private async Task<FeedbackMute?> MutedForSpamAsync(Feedback marked, string by, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (marked.Sender == ForgetFeedbackAccount.Gone || await mutes.FindAsync(marked.Sender, marked.Account, now, cancellationToken) is not null)
        {
            return null;
        }

        var marks = (await AllAsync(cancellationToken))
            .Count(one => one.Sender == marked.Sender && one.IsSpam && one.ClosedAt is { } closed && now - closed <= FeedbackMute.SpamWindow);
        if (marks < FeedbackMute.SpamMarks)
        {
            return null;
        }

        var mute = FeedbackMute.Begun(now, by, FeedbackMute.BySpam);
        return await mutes.SetAsync(marked.Sender, marked.Account, mute, cancellationToken) ? mute : null;
    }

    private async Task<Result<Feedback>> KindAsync(string id, FeedbackKind kind, CancellationToken cancellationToken)
    {
        if (await FindAsync(id, cancellationToken) is not { } feedback)
        {
            return Result.Failure<Feedback>(FeedbackErrors.NotFound(id));
        }

        if (feedback.Kind == kind)
        {
            return Result.Success(feedback);
        }

        if (feedback.IsClosed)
        {
            return Result.Failure<Feedback>(FeedbackErrors.Closed);
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
        return Result.Success(changed);
    }

    private async Task<Result> SenderOfAsync(string id, CancellationToken cancellationToken) => await FindAsync(id, cancellationToken) switch
    {
        null => Result.Failure(FeedbackErrors.NotFound(id)),
        { Sender: ForgetFeedbackAccount.Gone } => Result.Failure(FeedbackErrors.NoSender),
        _ => Result.Success(),
    };

    private async Task<List<Feedback>> AllAsync(CancellationToken cancellationToken)
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
