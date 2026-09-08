using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A player gave up the rest of their evolution picks for the round.
/// </summary>
public sealed record EvolutionPassed(MatchId MatchId, RoundId Round, PlayerSlot Slot) : IDomainEvent;
