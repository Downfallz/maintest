using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A player ordered their own tied creatures among the places their side won (ADR 0063). First to act first.
/// </summary>
public sealed record TieOrderSubmitted(MatchId MatchId, RoundId RoundId, PlayerSlot Slot, IReadOnlyList<CreatureId> Order) : IMatchEvent;
