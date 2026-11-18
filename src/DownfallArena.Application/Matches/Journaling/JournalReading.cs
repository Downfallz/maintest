using DownfallArena.Application.Matches.Driving;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>A journal as it was read: its entries in order, and where it ended then.</summary>
public sealed record JournalReading(IReadOnlyList<IJournalEntry> Entries, JournalPosition Position)
{
    /// <summary>
    /// True when the store holds no journal for the run, as opposed to a journal with nothing in it (#874). A recorded
    /// table's journal is created empty with its run, so a holder that read it and now finds it absent knows the run
    /// was deleted. An unrecorded table's journal is absent until its first line.
    /// </summary>
    public bool Absent { get; init; }

    /// <summary>No journal in the store, read from <paramref name="after" />: nothing, and the position unchanged.</summary>
    public static JournalReading AbsentAt(JournalPosition after) => new([], after) { Absent = true };

    /// <summary>The decisions among the entries, in order.</summary>
    public IReadOnlyList<SeatDecision> Decisions => [.. Entries.OfType<DecisionEntry>().Select(entry => entry.Decision)];
}
