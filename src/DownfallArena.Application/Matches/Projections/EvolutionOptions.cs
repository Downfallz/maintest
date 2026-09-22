namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// The picks a player has left and, per creature that can still buy something, the packages available to it.
/// Passing is always allowed. What each package contains is in the catalogue, not here: this is the legal
/// move list, and it is the domain's answer rather than a client's own reading of the schedule.
/// </summary>
public sealed record EvolutionOptions(int RemainingPicks, IReadOnlyList<EvolutionOption> Creatures);
