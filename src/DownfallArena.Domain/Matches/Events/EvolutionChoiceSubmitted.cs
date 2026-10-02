using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A player picked a package for one of their creatures. The pick is face down: nothing is bought until the
/// sub-phase ends and <see cref="PurchasesRevealed"/> buys every pick at once (ADR 0089).
/// </summary>
public sealed record EvolutionChoiceSubmitted(MatchId MatchId, RoundId RoundId, PlayerSlot Slot, EvolutionChoice Choice) : IMatchEvent;
