using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Commands;

/// <summary>A player's own tied creatures, first to act first (ADR 0063).</summary>
public sealed record SubmitTieOrder(MatchId MatchId, PlayerSlot Slot, IReadOnlyList<CreatureId> Order);
