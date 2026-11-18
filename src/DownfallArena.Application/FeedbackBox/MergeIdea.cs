using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>An idea merged into another (docs/tabletop/feedback-plan.md, I1).</summary>
public sealed class MergeIdea(IFeedbackStore store, FeedbackWrites writes)
{
    /// <summary>
    /// Merges an idea into another: it leaves the list and is read inside that one, its sender and words kept, and counts
    /// towards how many asked. An idea merged since is followed to the one its group became. Only an open idea is merged,
    /// into another open idea and never into itself or its own group. Merged again into the same group, nothing changes.
    /// </summary>
    public Task<Result<FeedbackMerge>> HandleAsync(string id, string into, CancellationToken cancellationToken = default) =>
        writes.OneAtATimeAsync(() => MergingAsync(id, into, cancellationToken), cancellationToken);

    private async Task<Result<FeedbackMerge>> MergingAsync(string id, string into, CancellationToken cancellationToken)
    {
        if (await FeedbackRecords.FindAsync(store, id, cancellationToken) is not { } idea)
        {
            return Result.Failure<FeedbackMerge>(FeedbackErrors.NotFound(id));
        }

        if (await FeedbackRecords.FindAsync(store, into, cancellationToken) is not { } target || await FeedbackGroups.RootAsync(store, target, cancellationToken) is not { } root)
        {
            return Result.Failure<FeedbackMerge>(FeedbackErrors.NotFound(into));
        }

        if (await AgainAsync(idea, root, cancellationToken) is { } again)
        {
            return again;
        }

        // The group's list is written first, as the checks read it: a write cut short leaves it naming an idea too many,
        // which they skip, never one too few. Then the ideas that joined this one name the group's idea themselves, so
        // groups stay flat; then this one; and last the group it left lets it go.
        var grown = root with { Joined = [.. root.Joined.Append(idea.Id).Concat(idea.Joined).Distinct(StringComparer.Ordinal)] };
        await store.SaveAsync(grown, cancellationToken);
        foreach (var joinedId in idea.Joined)
        {
            if (await FeedbackRecords.FindAsync(store, joinedId, cancellationToken) is { } joined && joined.MergedInto == idea.Id)
            {
                await store.SaveAsync(joined with { MergedInto = root.Id }, cancellationToken);
            }
        }

        // Merging is reading it: a new idea merged no longer waits in the Overview's count.
        var merged = idea with { MergedInto = root.Id, Joined = [], Status = idea.Status == FeedbackStatus.New ? FeedbackStatus.Seen : idea.Status };
        await store.SaveAsync(merged, cancellationToken);
        if (idea.MergedInto is { } left && left != root.Id)
        {
            await FeedbackGroups.LeaveAsync(store, left, idea.Id, cancellationToken);
        }

        return Result.Success(new FeedbackMerge(merged, grown, Changed: true));
    }

    /// <summary>
    /// What a merge answers before it writes the merge itself: why it is refused, or, merged into this group already,
    /// that nothing changed, the group's list finished first when a cut left the idea off it. None when it is to merge.
    /// </summary>
    private async Task<Result<FeedbackMerge>?> AgainAsync(Feedback idea, Feedback root, CancellationToken cancellationToken)
    {
        if (idea.Kind != FeedbackKind.Idea || root.Kind != FeedbackKind.Idea)
        {
            return Result.Failure<FeedbackMerge>(FeedbackErrors.Invalid("Only an idea is merged, and only into another idea."));
        }

        if (root.Id == idea.Id)
        {
            return Result.Failure<FeedbackMerge>(FeedbackErrors.Invalid("An idea is not merged into itself, nor into an idea merged into it."));
        }

        if (idea.MergedInto is not null && (await FeedbackGroups.RootAsync(store, idea, cancellationToken))?.Id == root.Id)
        {
            if (root.Joined.Contains(idea.Id, StringComparer.Ordinal))
            {
                return Result.Success(new FeedbackMerge(idea, root, Changed: false));
            }

            var finished = root with { Joined = [.. root.Joined, idea.Id] };
            await store.SaveAsync(finished, cancellationToken);
            return Result.Success(new FeedbackMerge(idea, finished, Changed: true));
        }

        return idea.IsClosed || root.IsClosed ? Result.Failure<FeedbackMerge>(FeedbackErrors.Closed) : null;
    }
}
