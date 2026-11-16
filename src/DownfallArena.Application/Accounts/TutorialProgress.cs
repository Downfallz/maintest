namespace DownfallArena.Application.Accounts;

/// <summary>
/// What an account keeps of the Tutorials (ADR 0187): the ones it finished and the ones it skipped, by their id,
/// kept with its game settings. A Tutorial in neither is new to the account, and only a new one opens by itself.
/// </summary>
public sealed record TutorialProgress
{
    private TutorialProgress(IReadOnlyList<string> done, IReadOnlyList<string> skipped)
    {
        Done = done;
        Skipped = skipped;
    }

    /// <summary>An account that has neither finished nor skipped any Tutorial.</summary>
    public static TutorialProgress None { get; } = new([], []);

    /// <summary>The Tutorials finished, in the order of their ids.</summary>
    public IReadOnlyList<string> Done { get; }

    /// <summary>The Tutorials skipped and not finished since, in the order of their ids.</summary>
    public IReadOnlyList<string> Skipped { get; }

    /// <summary>The progress a store read: each id once, and a Tutorial that is done is not also skipped.</summary>
    public static TutorialProgress Of(IEnumerable<string>? done, IEnumerable<string>? skipped)
    {
        var finished = Ids(done);
        return new TutorialProgress(finished, [.. Ids(skipped).Except(finished, StringComparer.Ordinal)]);
    }

    public TutorialState StateOf(string tutorial) =>
        Done.Contains(tutorial, StringComparer.Ordinal) ? TutorialState.Done
        : Skipped.Contains(tutorial, StringComparer.Ordinal) ? TutorialState.Skipped
        : TutorialState.New;

    /// <summary>The same progress with this Tutorial finished: it is no longer one that was skipped.</summary>
    public TutorialProgress WithDone(string tutorial) => Of(Done.Append(tutorial), Skipped);

    /// <summary>The same progress with this Tutorial skipped; one already finished stays finished.</summary>
    public TutorialProgress WithSkipped(string tutorial) => Of(Done, Skipped.Append(tutorial));

    public bool Equals(TutorialProgress? other) =>
        other is not null && Done.SequenceEqual(other.Done, StringComparer.Ordinal) && Skipped.SequenceEqual(other.Skipped, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Done.Count, Skipped.Count);

    private static string[] Ids(IEnumerable<string>? ids) =>
        [.. (ids ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
