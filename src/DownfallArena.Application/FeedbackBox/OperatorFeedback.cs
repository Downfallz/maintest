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

    /// <summary>
    /// A page of what the filter keeps, newest first; ideas alone by how many asked, then by the newest ask, so what many
    /// players want comes first. A page past the last is the last.
    /// </summary>
    public async Task<FeedbackPage> PageAsync(FeedbackFilter filter, int page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var all = await AllAsync(cancellationToken);
        var asks = FeedbackGroups.Of(all)
            .Where(group => group.Value[0].Kind == FeedbackKind.Idea)
            .ToDictionary(group => group.Key, group => FeedbackGroups.AsksOf(group.Value), StringComparer.Ordinal);
        var kept = all.Where(filter.Keeps).ToList();
        if (filter.Kind == FeedbackKind.Idea)
        {
            kept = [.. kept
                .OrderByDescending(one => asks.GetValueOrDefault(one.Id)?.AskedBy ?? 1)
                .ThenByDescending(one => asks.GetValueOrDefault(one.Id)?.Latest ?? one.LastWritten)
                .ThenByDescending(one => one.Id, StringComparer.Ordinal)];
        }

        var pages = Math.Max(1, (kept.Count + PerPage - 1) / PerPage);
        var shown = Math.Clamp(page, 1, pages);
        var rows = kept.Skip((shown - 1) * PerPage).Take(PerPage).ToList();
        return new FeedbackPage(rows, kept.Count, shown, pages, rows.Where(one => asks.ContainsKey(one.Id)).ToDictionary(one => one.Id, one => asks[one.Id], StringComparer.Ordinal));
    }

    /// <summary>
    /// An idea's group as its sheet reads it: the ideas merged into it and how many asked, or the idea it was merged into.
    /// A bug report is never grouped, so it reads nothing more.
    /// </summary>
    public async Task<FeedbackGroup> GroupAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        if (feedback.Kind == FeedbackKind.Bug)
        {
            return new FeedbackGroup(null, [], FeedbackGroups.AsksOf([feedback]));
        }

        if (feedback.MergedInto is not null)
        {
            return new FeedbackGroup(await FeedbackGroups.RootAsync(store, feedback, cancellationToken), [], FeedbackGroups.AsksOf([feedback]));
        }

        var group = FeedbackGroups.Of(await AllAsync(cancellationToken)).GetValueOrDefault(feedback.Id) ?? [feedback];
        return new FeedbackGroup(null, [.. group.Where(one => one.Id != feedback.Id)], FeedbackGroups.AsksOf(group));
    }

    /// <summary>
    /// Merges an idea into another: it leaves the list and is read inside that one, its sender and words kept, and counts
    /// towards how many asked. An idea merged since is followed to the one its group became. Only an open idea is merged,
    /// into another open idea and never into itself or its own group. Merged again into the same group, nothing changes.
    /// </summary>
    public Task<Result<FeedbackMerge>> MergeAsync(string id, string into, CancellationToken cancellationToken = default) =>
        writes.OneAtATimeAsync(() => MergingAsync(id, into, cancellationToken), cancellationToken);

    /// <summary>Marks an idea planned, with the path of the plan it went into (<see cref="PlanPath" />); that reopens it when it was closed.</summary>
    public Task<Result<FeedbackStatusChange>> PlanAsync(string id, string? plan, CancellationToken cancellationToken = default) =>
        writes.OneAtATimeAsync(() => StatusAsync(id, FeedbackStatus.Planned, null, plan?.Trim(), cancellationToken), cancellationToken);

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
    /// Marks it seen, which reopens it when it was closed; reproduced, for a bug report; shipped or declined, for an idea,
    /// declining with a reason its players read; or dismissed, with a reason of one line,
    /// <see cref="Feedback.SpamReason" /> marking it as spam. Planned is <see cref="PlanAsync" />'s, since it names a plan.
    /// The status it already has, with the same reason, changes nothing. An idea merged into another takes none.
    /// </summary>
    public async Task<Result<FeedbackStatusChange>> SetStatusAsync(string id, FeedbackStatus status, string? reason, string by, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        var set = await writes.OneAtATimeAsync(() => StatusAsync(id, status, reason?.Trim(), plan: null, cancellationToken), cancellationToken);
        if (set.IsFailure || !set.Value.Feedback.IsSpam || set.Value.Feedback.SpamCounted)
        {
            return set;
        }

        // Counted outside the gate, then marked counted: a failure in between leaves it to count when asked again.
        var muted = await MutedForSpamAsync(set.Value.Feedback, by, cancellationToken);
        var counted = await writes.OneAtATimeAsync(() => CountedAsync(id, cancellationToken), cancellationToken);
        return Result.Success(set.Value with { Feedback = counted ?? set.Value.Feedback, Muted = muted });
    }

    /// <summary>Marks a spam mark counted, as it is now; nothing when it is no longer one.</summary>
    private async Task<Feedback?> CountedAsync(string id, CancellationToken cancellationToken)
    {
        if (await FindAsync(id, cancellationToken) is not { IsSpam: true, SpamCounted: false } marked)
        {
            return null;
        }

        var counted = marked with { SpamCounted = true };
        await store.SaveAsync(counted, cancellationToken);
        return counted;
    }

    /// <summary>
    /// A bug report the rules answered as designed becomes an idea, or an idea that names a rule gone wrong a bug report;
    /// its words, position and sender kept. An idea others were merged into stays one: a bug report is never grouped.
    /// Whether any were is read before the write gate, as the spam count is, so players' sending never waits on it.
    /// </summary>
    public async Task<Result<FeedbackKindChange>> ChangeKindAsync(string id, FeedbackKind kind, CancellationToken cancellationToken = default)
    {
        if (kind == FeedbackKind.Bug && (await AllAsync(cancellationToken)).Any(one => one.MergedInto == id))
        {
            return Result.Failure<FeedbackKindChange>(FeedbackErrors.Grouped);
        }

        return await writes.OneAtATimeAsync(() => KindAsync(id, kind, cancellationToken), cancellationToken);
    }

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

    private async Task<Result<FeedbackStatusChange>> StatusAsync(string id, FeedbackStatus status, string? reason, string? plan, CancellationToken cancellationToken)
    {
        if (await FindAsync(id, cancellationToken) is not { } feedback)
        {
            return Result.Failure<FeedbackStatusChange>(FeedbackErrors.NotFound(id));
        }

        if (feedback.MergedInto is not null)
        {
            return Result.Failure<FeedbackStatusChange>(FeedbackErrors.MergedAway);
        }

        var now = clock.GetUtcNow();
        var idea = feedback.Kind == FeedbackKind.Idea;
        var oneLine = reason is { Length: > 0 and <= LongestReason } && !reason.Any(char.IsControl);

        // Closed again with the status it has keeps when it was closed: the 180 days run from the first time.
        var closedAt = feedback.Status == status ? feedback.ClosedAt ?? now : now;
        var reopened = feedback with { Status = status, ClosedAt = null, Reason = null, Plan = null, SpamCounted = false };
        Feedback changed;
        switch (status)
        {
            case FeedbackStatus.Seen:
            case FeedbackStatus.Reproduced when !idea:
                changed = reopened;
                break;
            case FeedbackStatus.Planned when idea && PlanPath.Is(plan):
                changed = reopened with { Plan = plan };
                break;
            case FeedbackStatus.Planned when idea:
                return Result.Failure<FeedbackStatusChange>(FeedbackErrors.Invalid("Planning names the plan's path in the repository: docs/…-plan.md."));
            case FeedbackStatus.Shipped when idea:
                changed = reopened with { ClosedAt = closedAt, Plan = feedback.Plan };
                break;
            case FeedbackStatus.Dismissed when oneLine:
            case FeedbackStatus.Declined when idea && oneLine:
                changed = reopened with { ClosedAt = closedAt, Reason = reason };
                break;
            case FeedbackStatus.Dismissed:
            case FeedbackStatus.Declined when idea:
                return Result.Failure<FeedbackStatusChange>(FeedbackErrors.Invalid($"{(idea && status == FeedbackStatus.Declined ? "Declining" : "Dismissing")} says why, on one line of {LongestReason} characters at most."));
            default:
                return Result.Failure<FeedbackStatusChange>(FeedbackErrors.Invalid(idea
                    ? "An idea is marked seen, planned, shipped, declined or dismissed here."
                    : "A bug report is marked seen, reproduced or dismissed here."));
        }

        if (changed.Status == feedback.Status && changed.Reason == feedback.Reason && changed.Plan == feedback.Plan)
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

    private async Task<Result<FeedbackMerge>> MergingAsync(string id, string into, CancellationToken cancellationToken)
    {
        if (await FindAsync(id, cancellationToken) is not { } idea)
        {
            return Result.Failure<FeedbackMerge>(FeedbackErrors.NotFound(id));
        }

        if (await FindAsync(into, cancellationToken) is not { } target || await FeedbackGroups.RootAsync(store, target, cancellationToken) is not { } root)
        {
            return Result.Failure<FeedbackMerge>(FeedbackErrors.NotFound(into));
        }

        if (idea.Kind != FeedbackKind.Idea || root.Kind != FeedbackKind.Idea)
        {
            return Result.Failure<FeedbackMerge>(FeedbackErrors.Invalid("Only an idea is merged, and only into another idea."));
        }

        if (root.Id == idea.Id)
        {
            return Result.Failure<FeedbackMerge>(FeedbackErrors.Invalid("An idea is not merged into itself, nor into an idea merged into it."));
        }

        if (idea.MergedInto == root.Id)
        {
            return Result.Success(new FeedbackMerge(idea, root, Changed: false));
        }

        if (idea.IsClosed || root.IsClosed)
        {
            return Result.Failure<FeedbackMerge>(FeedbackErrors.Closed);
        }

        // Merging is reading it: a new idea merged no longer waits in the Overview's count.
        var merged = idea with { MergedInto = root.Id, Status = idea.Status == FeedbackStatus.New ? FeedbackStatus.Seen : idea.Status };
        await store.SaveAsync(merged, cancellationToken);
        return Result.Success(new FeedbackMerge(merged, root, Changed: true));
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
