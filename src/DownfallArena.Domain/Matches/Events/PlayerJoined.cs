using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A player took a slot in the match and their team was formed.
/// </summary>
public sealed record PlayerJoined(MatchId MatchId, PlayerSlot Slot, PlayerId Player) : IDomainEvent;
