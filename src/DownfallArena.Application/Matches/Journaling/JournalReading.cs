using DownfallArena.Application.Matches.Driving;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>A journal as it was read: its decisions in order, and where it ended then.</summary>
public sealed record JournalReading(IReadOnlyList<SeatDecision> Decisions, JournalPosition Position)
{
    public static JournalReading Empty { get; } = new([], JournalPosition.Start);
}
