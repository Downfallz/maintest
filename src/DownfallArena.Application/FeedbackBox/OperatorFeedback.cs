using DownfallArena.Application.Accounts;
using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The operator's side of feedback (docs/tabletop/feedback-plan.md, R3, ADR 0189): the Feedback tab's list, a sheet,
/// the status set by hand, a bug report turned into an idea and back, and the sender muted or let speak again. Three
/// pieces dismissed as spam within 30 days mute their sender on their own. Each change is idempotent: asked again, it
/// writes nothing and says it changed nothing. Every write to feedback goes through <see cref="FeedbackWrites" />, so a
/// status set while the sender adds to the same feedback loses neither; reading every record, as counting spam marks
/// does, happens outside it, so the players' sending never waits on it.
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

    /// <summary>Whether the feedback's sender can be muted: one is kept for it, and its account was not deleted.</summary>
    public bool CanMute(Feedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        return feedback.Sender != ForgetFeedbackAccount.Gone && mutes.CanHold(feedback.Account);
    }

    /// <summary>
    /// Marks it seen, which reopens it when it was dismissed; reproduced, for a bug report; or dismissed, with a reason of
    /// one line, <see cref="Feedback.SpamReason" /> marking it as spam. The status it already has, with the same reason,
    /// changes nothing.
    /// </summary>
    public async Task<Result<FeedbackStatusChange>> SetStatusAsync(string id, FeedbackStatus status, string? reason, string by, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        var set = await writes.OneAtATimeAsync(() => StatusAsync(id, status, reason?.Trim(), cancellationToken), cancellationToken);
        if (set.IsFailure || !set.Value.Changed || !set.Value.Feedback.IsSpam)
        {
            return set;
        }

        return Result.Success(set.Value with { Muted = await MutedForSpamAsync(set.Value.Feedback, by, cancellationToken) });
    }

    /// <summary>A bug report the rules answered as designed becomes an idea, or an idea that names a rule gone wrong a bug report; its words, position and sender kept.</summary>
    public Task<Result<FeedbackKindChange>> ChangeKindAsync(string id, FeedbackKind kind, CancellationToken cancellationToken = default) =>
        writes.OneAtATimeAsync(() => KindAsync(id, kind, cancellationToken), cancellationToken);

    /// <summary>Stops the feedback's sender from sending more for <see cref="FeedbackMute.Lasts" />; a mute that holds already is kept as it is.</summary>
    public async Task<Result<FeedbackMuteChange>> MuteAsync(string id, string by, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        var sender = await MuteableAsync(id, cancellationToken);
        if (sender.IsFailure)
        {
            return Result.Failure<FeedbackMuteChange>(sender.Error);
        }

        var feedback = sender.Value;
        if (await MuteOfAsync(feedback, cancellationToken) is { } holding)
        {
            return Result.Success(new FeedbackMuteChange(holding, Changed: false));
        }

        var mute = FeedbackMute.Begun(clock.GetUtcNow(), by, FeedbackMute.ByOperator);
        await mutes.SetAsync(feedback.Sender, feedback.Account, mute, cancellationToken);
        return Result.Success(new FeedbackMuteChange(mute, Changed: true));
    }

    /// <summary>Lets the feedback's sender send again; nothing changes for one no mute holds on.</summary>
    public async Task<Result<FeedbackMuteChange>> UnmuteAsync(string id, CancellationToken cancellationToken = default)
    {
        var sender = await MuteableAsync(id, cancellationToken);
        if (sender.IsFailure)
        {
            return Result.Failure<FeedbackMuteChange>(sender.Error);
        }

        var feedback = sender.Value;
        if (await MuteOfAsync(feedback, cancellationToken) is null)
        {
            return Result.Success(new FeedbackMuteChange(null, Changed: false));
        }

        await mutes.SetAsync(feedback.Sender, feedback.Account, null, cancellationToken);
        return Result.Success(new FeedbackMuteChange(null, Changed: true));
    }

    /// <summary>What the Overview's card counts.</summary>
    public async Task<FeedbackWaiting> WaitingAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var lastDay = (await store.IdsAsync(cancellationToken)).Count(id => Feedback.SentAt(id) is { } at && at > now - SendFeedback.Day);
        var waiting = (await AllAsync(cancellationToken)).Where(one => one.Status == FeedbackStatus.New).ToList();
        return new FeedbackWaiting(waiting.Count, waiting.Count(one => one.Kind == FeedbackKind.Bug), lastDay, lastDay >= SendFeedback.PerHost);
    }

    private async Task<Result<FeedbackStatusChange>> StatusAsync(string id, FeedbackStatus status, string? reason, CancellationToken cancellationToken)
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
            case FeedbackStatus.Reproduced when feedback.Kind == FeedbackKind.Bug:
                changed = feedback with { Status = status, ClosedAt = null, Reason = null };
                break;
            case FeedbackStatus.Dismissed when reason is { Length: > 0 and <= LongestReason } && !reason.Any(char.IsControl):
                // Dismissed again for another reason keeps when it was closed: the 180 days run from the first time.
                changed = feedback with { Status = status, ClosedAt = feedback.Status == FeedbackStatus.Dismissed ? feedback.ClosedAt ?? now : now, Reason = reason };
                break;
            case FeedbackStatus.Dismissed:
                return Result.Failure<FeedbackStatusChange>(FeedbackErrors.Invalid($"Dismissing says why, on one line of {LongestReason} characters at most."));
            default:
                return Result.Failure<FeedbackStatusChange>(FeedbackErrors.Invalid(feedback.Kind == FeedbackKind.Bug
                    ? "A bug report is marked seen, reproduced or dismissed here."
                    : "An idea is marked seen or dismissed here."));
        }

        if (changed.Status == feedback.Status && changed.Reason == feedback.Reason)
        {
            return Result.Success(new FeedbackStatusChange(feedback, Changed: false, Muted: null));
        }

        await store.SaveAsync(changed, cancellationToken);
        return Result.Success(new FeedbackStatusChange(changed, Changed: true, Muted: null));
    }

    /// <summary>The mute a third spam mark within 30 days earns its sender, or none when it is not the third or a mute holds already.</summary>
    private async Task<FeedbackMute?> MutedForSpamAsync(Feedback marked, string by, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (!CanMute(marked) || await mutes.FindAsync(marked.Sender, marked.Account, now, cancellationToken) is not null)
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
        await mutes.SetAsync(marked.Sender, marked.Account, mute, cancellationToken);
        return mute;
    }

    private async Task<Result<FeedbackKindChange>> KindAsync(string id, FeedbackKind kind, CancellationToken cancellationToken)
    {
        if (await FindAsync(id, cancellationToken) is not { } feedback)
        {
            return Result.Failure<FeedbackKindChange>(FeedbackErrors.NotFound(id));
        }

        if (feedback.Kind == kind)
        {
            return Result.Success(new FeedbackKindChange(feedback, Was: null));
        }

        if (feedback.IsClosed)
        {
            return Result.Failure<FeedbackKindChange>(FeedbackErrors.Closed);
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

    /// <summary>The feedback, read once, when its sender can be muted; or why not.</summary>
    private async Task<Result<Feedback>> MuteableAsync(string id, CancellationToken cancellationToken) => await FindAsync(id, cancellationToken) switch
    {
        null => Result.Failure<Feedback>(FeedbackErrors.NotFound(id)),
        { } found when !CanMute(found) => Result.Failure<Feedback>(FeedbackErrors.NoSender),
        { } found => Result.Success(found),
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
