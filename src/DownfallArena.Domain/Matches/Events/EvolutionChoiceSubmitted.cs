using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Events;

/// <summary>
/// A player picked a package for one of their creatures. The pick is face down: nothing is bought until the
/// Purchase reveal at the end of the sub-phase, which <see cref="PurchasesRevealed"/> records (ADR 0089).
/// </summary>
public sealed record EvolutionChoiceSubmitted(MatchId MatchId, RoundId RoundId, PlayerSlot Slot, EvolutionChoice Choice) : IMatchEvent;
