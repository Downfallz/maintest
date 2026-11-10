using DownfallArena.Application.Matches.Driving;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>A decision a seat took, whoever sat there.</summary>
public sealed record DecisionEntry(SeatDecision Decision) : IJournalEntry;
