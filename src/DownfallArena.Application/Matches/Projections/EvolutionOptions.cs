namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// The picks a player has left and, per creature that can still unlock something, the unlockable spells.
/// Passing is always allowed.
/// </summary>
public sealed record EvolutionOptions(int RemainingPicks, IReadOnlyList<EvolutionOption> Creatures);
