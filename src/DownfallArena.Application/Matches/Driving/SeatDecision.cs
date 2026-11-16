using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// A decision and the seat that made it, whoever sits there. In the order they were taken these are what a
/// table writes down, and what rebuilds it when they are submitted again (ADR 0159).
/// </summary>
public sealed record SeatDecision(PlayerSlot Slot, PlayerDecision Decision);
