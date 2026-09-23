using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A player bought a package for one of their creatures: every spell in it, and its initiative bonus.
/// </summary>
public sealed record EvolutionChoiceSubmitted(MatchId MatchId, RoundId RoundId, PlayerSlot Slot, EvolutionChoice Choice) : IMatchEvent;
