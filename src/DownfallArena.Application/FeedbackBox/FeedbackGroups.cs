using DownfallArena.Application.FeedbackBox.Ports;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// Ideas grouped by merging (docs/tabletop/feedback-plan.md, I1). A merged idea names the one it joined, which may have
/// been merged since: the group's idea is the end of that chain, followed at each read, so a merge writes one record.
/// A chain that loops or names feedback no longer kept ends nowhere.
/// </summary>
internal static class FeedbackGroups
{
    /// <summary>The idea the feedback's group became, read from the store; itself when it was never merged.</summary>
    public static async Task<Feedback?> RootAsync(IFeedbackStore store, Feedback feedback, CancellationToken cancellationToken)
    {
        var passed = new HashSet<string>(StringComparer.Ordinal);
        var at = feedback;
        while (at.MergedInto is { } next)
        {
            if (!passed.Add(at.Id) || await store.FindAsync(next, cancellationToken) is not { } joined)
            {
                return null;
            }

            at = joined;
        }

        return at;
    }

    /// <summary>Every group among what is kept, by the id of the idea it became, that idea first then the rest as they came.</summary>
    public static Dictionary<string, List<Feedback>> Of(IReadOnlyList<Feedback> all)
    {
        var byId = all.ToDictionary(one => one.Id, StringComparer.Ordinal);
        var groups = new Dictionary<string, List<Feedback>>(StringComparer.Ordinal);
        foreach (var one in all.OrderBy(one => one.MergedInto is not null).ThenBy(one => one.At))
        {
            if (Root(one, byId) is { } root)
            {
                if (!groups.TryGetValue(root.Id, out var group))
                {
                    groups[root.Id] = group = [];
                }

                group.Add(one);
            }
        }

        return groups;
    }

    /// <summary>How many asked: each sender once, but each deleted account's feedback apart, since they were different players.</summary>
    public static FeedbackAsks AsksOf(IReadOnlyCollection<Feedback> group) => new(
        group.Select(one => one.Sender == ForgetFeedbackAccount.Gone ? $"{ForgetFeedbackAccount.Gone}:{one.Id}" : one.Sender).Distinct(StringComparer.Ordinal).Count(),
        group.Min(one => one.At),
        group.Max(one => one.LastWritten));

    private static Feedback? Root(Feedback feedback, Dictionary<string, Feedback> byId)
    {
        var passed = new HashSet<string>(StringComparer.Ordinal);
        var at = feedback;
        while (at.MergedInto is { } next)
        {
            if (!passed.Add(at.Id) || !byId.TryGetValue(next, out var joined))
            {
                return null;
            }

            at = joined;
        }

        return at;
    }
}
