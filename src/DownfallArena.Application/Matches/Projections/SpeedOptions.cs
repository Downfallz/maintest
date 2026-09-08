using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// The player's creatures that still need a speed choice.
/// </summary>
public sealed record SpeedOptions(IReadOnlyList<CreatureId> Missing);
