using DownfallArena.Application.FeedbackBox.Ports;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>An idea merged by mistake put back in the list as one of its own (docs/tabletop/feedback-plan.md, I1, #725).</summary>
public sealed class UnmergeIdea(IFeedbackStore store, FeedbackWrites writes)
{
    /// <summary>
    /// Takes a merged idea out of its group: it is listed again as an idea of its own, keeps its status, and no longer
    /// counts towards the group's asks. An idea not merged, or unmerged already, changes nothing.
    /// </summary>
    public Task<Result<FeedbackUnmerge>> HandleAsync(string id, CancellationToken cancellationToken = default) =>
        writes.OneAtATimeAsync(() => UnmergingAsync(id, cancellationToken), cancellationToken);

    private async Task<Result<FeedbackUnmerge>> UnmergingAsync(string id, CancellationToken cancellationToken)
    {
        if (await FeedbackRecords.FindAsync(store, id, cancellationToken) is not { } idea)
        {
            return Result.Failure<FeedbackUnmerge>(FeedbackErrors.NotFound(id));
        }

        if (idea.MergedInto is not { } group)
        {
            return Result.Success(new FeedbackUnmerge(idea, From: null, Changed: false));
        }

        // The idea first, then the group's list, as a merge leaves a group: a write cut short leaves the list naming an
        // idea that no longer names the group, which the reads skip, never an idea the list lost.
        var unmerged = idea with { MergedInto = null };
        await store.SaveAsync(unmerged, cancellationToken);
        await FeedbackGroups.LeaveAsync(store, group, id, cancellationToken);
        return Result.Success(new FeedbackUnmerge(unmerged, group, Changed: true));
    }
}
