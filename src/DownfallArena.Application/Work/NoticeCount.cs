namespace DownfallArena.Application.Work;

/// <summary>
/// What the notices worker did with one kind of notice (notifications plan, N6): how many reached a device, how many
/// tries a push service refused or did not answer, and how many were for a device that is gone. A kind is the push
/// topic the door sends it under (<c>turn</c>, <c>friend</c>, <c>invitation</c>, <c>match</c>).
/// </summary>
public sealed record NoticeCount(string Kind, long Sent, long Refused, long Gone)
{
    /// <summary>Whether it counts nothing at all.</summary>
    public bool IsEmpty => Sent == 0 && Refused == 0 && Gone == 0;

    /// <summary>The counts added together, a kind each, in the order of the kinds' names; a kind that counts nothing is left out.</summary>
    public static IReadOnlyList<NoticeCount> Sum(IEnumerable<NoticeCount> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        return [.. counts
            .GroupBy(count => count.Kind, StringComparer.Ordinal)
            .Select(kind => new NoticeCount(kind.Key, kind.Sum(count => count.Sent), kind.Sum(count => count.Refused), kind.Sum(count => count.Gone)))
            .Where(count => !count.IsEmpty)
            .OrderBy(count => count.Kind, StringComparer.Ordinal)];
    }
}
