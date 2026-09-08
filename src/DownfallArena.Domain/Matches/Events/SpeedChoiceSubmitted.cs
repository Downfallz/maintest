using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A player chose the speed of one of their creatures.
/// </summary>
public sealed record SpeedChoiceSubmitted(MatchId MatchId, RoundId RoundId, PlayerSlot Slot, SpeedChoice Choice) : IDomainEvent;
