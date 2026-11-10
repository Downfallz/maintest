using DownfallArena.Application.Matches.Driving;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>A journal as it was read: its entries in order, and where it ended then.</summary>
public sealed record JournalReading(IReadOnlyList<IJournalEntry> Entries, JournalPosition Position)
{
    public static JournalReading Empty { get; } = new([], JournalPosition.Start);

    /// <summary>The decisions among the entries, in order.</summary>
    public IReadOnlyList<SeatDecision> Decisions => [.. Entries.OfType<DecisionEntry>().Select(entry => entry.Decision)];
}
