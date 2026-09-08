using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A player unlocked a spell for one of their creatures.
/// </summary>
public sealed record EvolutionChoiceSubmitted(MatchId MatchId, RoundId Round, PlayerSlot Slot, EvolutionChoice Choice) : IDomainEvent;
