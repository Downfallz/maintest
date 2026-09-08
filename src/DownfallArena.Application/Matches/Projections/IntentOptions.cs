namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// The player's creatures on the timeline without an intent, each with the spells it can afford.
/// </summary>
public sealed record IntentOptions(IReadOnlyList<IntentOption> Creatures);
