using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A player declared an intent. Hidden from the other player until revealed; the application layer decides who sees it.
/// </summary>
public sealed record IntentSubmitted(MatchId MatchId, RoundId RoundId, PlayerSlot Slot, CombatIntent Intent) : IDomainEvent;
